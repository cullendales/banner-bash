using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace GameServer
{
	class Client
	{
		// different packet types to be sent and received
		public enum PacketType : byte
		{
			Welcome = 1,
			PlayerPosition = 2,
			PlayerState = 3,
			FlagUpdate = 4,
			PlayerJoined = 5,
			PlayerLeft = 6,
			Attack = 7,
			TakeHit = 8,
			SlotRequest = 9,
			RequestFlagPickup = 10,
			RequestFlagDrop = 11,
			PlayerScore = 12,
			GameWon = 13
		}

		public int id;
		public TCP tcp;

		public Client(int _clientId)
		{
			id = _clientId; // unique id for client
			tcp = new TCP(id);
		}

		// handles connection, packet processing, and data receiving & sending 
		public class TCP
		{
			public TcpClient? socket;
			private readonly int id;
			private NetworkStream? stream;
			private byte[]? receiveBuffer;
			private const int dataBufferSize = 4096;

			public TCP(int _id)
			{
				id = _id;
			}

			public void Connect(TcpClient _socket)
			{
				socket = _socket;
				socket.SendBufferSize = dataBufferSize;
				socket.ReceiveBufferSize = dataBufferSize; // fix: receive, not send twice

				stream = socket.GetStream();
				receiveBuffer = new byte[dataBufferSize];
				stream = socket.GetStream();
				receiveBuffer = new byte[dataBufferSize];

				stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);

				// Update player count
				Server.ConnectedPlayers++;
				Console.WriteLine($"Player {id} connected. Total players: {Server.ConnectedPlayers}");

				// Send welcome packet to the client
				SendWelcome();

				// Send existing players to the new client
				SendExistingPlayers();

				// Broadcast player joined to all clients
				BroadcastPlayerJoined();
			}

			private void SendWelcome()
			{
				using (MemoryStream ms = new MemoryStream())
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					w.Write((byte)PacketType.Welcome);
					w.Write(id);
					SendData(ms.ToArray());
				}

				Console.WriteLine($"Sent welcome packet to client {id}");
			}

			private void SendExistingPlayers()
			{
				Console.WriteLine($"Sending existing players to client {id}");

				foreach (var client in Server.clients.Values)
				{
					if (client.id == id || client.tcp.socket == null || !client.tcp.socket.Connected)
						continue;

					using (MemoryStream ms = new MemoryStream())
					using (BinaryWriter w = new BinaryWriter(ms))
					{
						w.Write((byte)PacketType.PlayerJoined);
						w.Write(client.id);
						w.Write(Server.ConnectedPlayers);
						SendData(ms.ToArray());
					}
				}
			}

			private void ReceiveCallback(IAsyncResult _result)
			{
				try
				{
					if (stream == null || receiveBuffer == null) return;


					int _byteLength = stream.EndRead(_result);
					if (_byteLength <= 0)
					{
						Console.WriteLine($"Client {id} disconnected");
						Disconnect();
						return;
					}

					byte[] _data = new byte[_byteLength];
					Array.Copy(receiveBuffer, _data, _byteLength);

					HandlePacket(_data);


					if (stream != null && receiveBuffer != null)
					{
						stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
					}
				}
				catch (Exception _ex)
				{
					Console.WriteLine($"Error receiving TCP data from client {id}: {_ex}");
					Disconnect();
				}
			}
			// received different packets with types and then forwards that data to the other clients
			private void HandlePacket(byte[] data)
			{
				try
				{
					using (MemoryStream ms = new MemoryStream(data))
					using (BinaryReader r = new BinaryReader(ms))
					{
						byte packetType = r.ReadByte();
						// Console.WriteLine($"[Client {id}] Packet {packetType} len={data.Length}");

						switch (packetType)
						{
							case (byte)PacketType.Welcome:
							{
								int clientId = r.ReadInt32();
								Console.WriteLine($"Client {id} sent welcome with ID: {clientId}");
								break;
							}

							case (byte)PacketType.SlotRequest:
							{
								int requestedSlot = r.ReadInt32();
								Console.WriteLine($"Client {id} requested slot {requestedSlot} - not implemented");
								break;
							}

							case (byte)PacketType.PlayerPosition:
							{
								float x = r.ReadSingle();
								float y = r.ReadSingle();
								float z = r.ReadSingle();
								float rotX = r.ReadSingle();
								float rotY = r.ReadSingle();
								float rotZ = r.ReadSingle();

								BroadcastPlayerPosition(id, x, y, z, rotX, rotY, rotZ);
								break;
							}

							case (byte)PacketType.PlayerState:
							{
								int hits = r.ReadInt32();
								bool isFlagHolder = r.ReadBoolean();
								float clientReportedScore = r.ReadSingle(); // ignore for authority
								float stamina = r.ReadSingle();
								int stringLength = r.ReadInt32();
								byte[] stringBytes = r.ReadBytes(stringLength);
								string animationState = Encoding.UTF8.GetString(stringBytes);

								// Do NOT trust/update score from client. Server is authoritative via ScoreTick().
								// (We still rebroadcast the state so other clients can animate.)
								BroadcastPlayerState(id, hits, isFlagHolder, clientReportedScore, stamina, animationState);
								break;
							}

							case (byte)PacketType.FlagUpdate:
							{
								// server-only; ignore client attempts
								Console.WriteLine($"[WARN] Client {id} sent FlagUpdate directly (ignored)");
								break;
							}

							case (byte)PacketType.RequestFlagDrop:
							{
								float dX = r.ReadSingle();
								float dY = r.ReadSingle();
								float dZ = r.ReadSingle();

								Console.WriteLine($"Client {id} requests drop at ({dX:F2}, {dY:F2}, {dZ:F2})");

								if (Server.FlagIsHeld && Server.CurrentFlagHolderId == id)
								{
									Server.FlagIsHeld = false;
									Server.CurrentFlagHolderId = -1;
									BroadcastFlagUpdate(id, false, dX, dY, dZ);
								}
								else
								{
									Console.WriteLine($"Drop ignored: server says player {id} does not hold flag");
								}
								break;
							}

							case (byte)PacketType.Attack:
							{
								float ax = r.ReadSingle();
								float ay = r.ReadSingle();
								float az = r.ReadSingle();
								BroadcastAttack(id, ax, ay, az);
								break;
							}

							case (byte)PacketType.TakeHit:
							{
								int targetPlayerId = r.ReadInt32();
								Console.WriteLine($"Player {id} hit Player {targetPlayerId}");

								// Broadcast damage event
								BroadcastTakeHit(id, targetPlayerId);

								// If target was holder, transfer to attacker (authoritative steal)
								if (Server.FlagIsHeld && Server.CurrentFlagHolderId == targetPlayerId)
								{
									Server.CurrentFlagHolderId = id;   // attacker now holds
									Server.FlagIsHeld = true;          // ensure ticking continues
									// announce pickup (pos not needed while attached)
									BroadcastFlagUpdate(id, true, 0f, 0f, 0f);
									Console.WriteLine($"Flag stolen! Player {id} is now holder (taken from {targetPlayerId})");
								}
								break;
							}

							case (byte)PacketType.RequestFlagPickup:
							{
								float fx = r.ReadSingle();
								float fy = r.ReadSingle();
								float fz = r.ReadSingle();

								Console.WriteLine($"Client {id} requests pickup near ({fx:F2}, {fy:F2}, {fz:F2})");

								if (!Server.FlagIsHeld)
								{
									Server.FlagIsHeld = true;
									Server.CurrentFlagHolderId = id;
									BroadcastFlagUpdate(id, true, fx, fy, fz);
								}
								else
								{
									Console.WriteLine($"Pickup denied: flag already held by {Server.CurrentFlagHolderId}");
								}
								break;
							}

							default:
								Console.WriteLine($"Unknown packet type: {packetType}");
								break;
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error handling packet from client {id}: {ex}");
					Console.WriteLine($"Packet data length: {data.Length}");
					if (data.Length > 0)
					{
						Console.WriteLine($"First byte: {data[0]}");
					}
				}
			}

			public void SendData(byte[] data)
			{
				if (socket != null && socket.Connected && stream != null)
				{
					stream.BeginWrite(data, 0, data.Length, SendCallback, null);
				}
			}
			// callback for operations of asynchronous data sending
			private void SendCallback(IAsyncResult ar)
			{
				try
				{
					stream?.EndWrite(ar);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error sending TCP data: {ex}");
				}
			}

			// ==== Broadcast helpers (server -> clients) ====

			private void BroadcastPlayerPosition(int playerId, float x, float y, float z, float rotX, float rotY, float rotZ)
			{
				using (MemoryStream ms = new MemoryStream())
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					w.Write((byte)PacketType.PlayerPosition);
					w.Write(playerId);
					w.Write(x); w.Write(y); w.Write(z);
					w.Write(rotX); w.Write(rotY); w.Write(rotZ);
					BroadcastToAll(ms.ToArray(), playerId);
				}
			}

			private void BroadcastPlayerState(int playerId, int hits, bool isFlagHolder, float score, float stamina, string animationState)
			{
				using (MemoryStream ms = new MemoryStream())
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					w.Write((byte)PacketType.PlayerState);
					w.Write(playerId);
					w.Write(hits);
					w.Write(isFlagHolder);
					w.Write(score);
					w.Write(stamina);
					byte[] str = Encoding.UTF8.GetBytes(animationState);
					w.Write(str.Length);
					w.Write(str);
					BroadcastToAll(ms.ToArray(), playerId);
				}
			}
			// updates all other clients of current player state
			private void BroadcastFlagUpdate(int playerId, bool isPickup, float x, float y, float z)
			{
				using (MemoryStream ms = new MemoryStream())
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					w.Write((byte)PacketType.FlagUpdate);
					w.Write(playerId);   // authoritative holder (on pickup) or dropper (on drop)
					w.Write(isPickup);
					w.Write(x); w.Write(y); w.Write(z);
					BroadcastToAll(ms.ToArray());
				}
			}
			// sends attack state to all other clients
			private void BroadcastAttack(int attackerId, float x, float y, float z)
			{
				using (MemoryStream ms = new MemoryStream())
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					w.Write((byte)PacketType.Attack);
					w.Write(attackerId);
					w.Write(x); w.Write(y); w.Write(z);
					BroadcastToAll(ms.ToArray(), attackerId);
				}
			}
			// broadcasts hits received
			private void BroadcastTakeHit(int attackerId, int targetPlayerId)
			{
				using (MemoryStream ms = new MemoryStream())
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					w.Write((byte)PacketType.TakeHit);
					w.Write(targetPlayerId);
					w.Write(1); // damage
					BroadcastToAll(ms.ToArray());
				}
			}
			// broadcasts data to all other clients
			private void BroadcastToAll(byte[] data, int excludeId = -1)
			{
				foreach (var client in Server.clients.Values)
				{
					if (client.id != excludeId && client.tcp.socket?.Connected == true)
					{
						client.tcp.SendData(data);
					}
				}
			}

			private void BroadcastPlayerJoined()
			{
				using (MemoryStream ms = new MemoryStream())
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					w.Write((byte)PacketType.PlayerJoined);
					w.Write(id);
					w.Write(Server.ConnectedPlayers);
					BroadcastToAll(ms.ToArray(), id);
				}
			}

			public void Disconnect()
			{
				if (socket != null && socket.Connected)
				{
					// If this player was the holder, clear server flag state (and optionally broadcast a drop)
					if (Server.FlagIsHeld && Server.CurrentFlagHolderId == id)
					{
						Server.FlagIsHeld = false;
						Server.CurrentFlagHolderId = -1;
						// Optional: broadcast a drop at (0,0,0) or last known pos if you track it
						BroadcastFlagUpdate(id, false, 0f, 0f, 0f);
					}

					Server.ConnectedPlayers--;
					Console.WriteLine($"Player {id} disconnected. Total players: {Server.ConnectedPlayers}");

					Server.RemovePlayerScore(id);
					BroadcastPlayerLeft();


					socket.Close();
					socket = null;
					Console.WriteLine($"Socket for player {id} set to null");
				}
				else
				{
					Console.WriteLine($"Player {id} disconnect called but socket was null or not connected");
				}
			}

			private void BroadcastPlayerLeft()
			{
				using (MemoryStream ms = new MemoryStream())
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					w.Write((byte)PacketType.PlayerLeft);
					w.Write(id);
					w.Write(Server.ConnectedPlayers);
					BroadcastToAll(ms.ToArray(), id);
				}
			}
		}
	}
}
