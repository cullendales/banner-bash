using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace GameServer
{
	class Client
	{
		public int id;
		public TCP tcp;

		public Client(int _clientId)
		{
			id = _clientId;
			tcp = new TCP(id);
		}

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
			socket.SendBufferSize = dataBufferSize;

			stream = socket.GetStream();
			receiveBuffer = new byte[dataBufferSize];

			stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
			
			// Update player count
			Server.ConnectedPlayers++;
			Console.WriteLine($"Player {id} connected. Total players: {Server.ConnectedPlayers}");
			
			// Send welcome packet to the client
			SendWelcome();
			
			// Broadcast player joined to all clients
			BroadcastPlayerJoined();
		}
		
		private void SendWelcome()
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write((byte)1); // Welcome packet
				writer.Write(id); // Client ID
				
				byte[] data = stream.ToArray();
				SendData(data);
			}
			
			Console.WriteLine($"Sent welcome packet to client {id}");
		}

			private void ReceiveCallback(IAsyncResult _result)
			{
				try
				{
					if (stream == null || receiveBuffer == null) return;
					
					int _byteLength = stream.EndRead(_result);
					if (_byteLength <= 0) 
					{ 
						// Client disconnected
						Console.WriteLine($"Client {id} disconnected");
						Disconnect();
						return; 
					}
					byte[] _data = new byte[_byteLength];
					Array.Copy(receiveBuffer, _data, _byteLength);
					
					// Handle the received packet
					HandlePacket(_data);
					
					stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
				}
				catch (Exception _ex)
				{
					Console.WriteLine($"Error receiving TCP data from client {id}: {_ex}");
					Disconnect();
				}
			}

			private void HandlePacket(byte[] data)
			{
				try
				{
					using (MemoryStream stream = new MemoryStream(data))
					using (BinaryReader reader = new BinaryReader(stream))
					{
											byte packetType = reader.ReadByte();
					Console.WriteLine($"Received packet type {packetType} from client {id} with {data.Length} bytes");
						
						switch (packetType)
						{
							case 1: // Welcome
								int clientId = reader.ReadInt32();
								Console.WriteLine($"Client {id} sent welcome with ID: {clientId}");
								break;
							
						case 2: // PlayerPosition
							float x = reader.ReadSingle();
							float y = reader.ReadSingle();
							float z = reader.ReadSingle();
							float rotX = reader.ReadSingle();
							float rotY = reader.ReadSingle();
							float rotZ = reader.ReadSingle();
							
							// Broadcast position to all other clients
							BroadcastPlayerPosition(id, x, y, z, rotX, rotY, rotZ);
							break;
							
						case 3: // PlayerState
							int hits = reader.ReadInt32();
							bool isFlagHolder = reader.ReadBoolean();
							float score = reader.ReadSingle();
							float stamina = reader.ReadSingle();
							int stringLength = reader.ReadInt32();
							byte[] stringBytes = reader.ReadBytes(stringLength);
							string animationState = Encoding.UTF8.GetString(stringBytes);
							
							// Broadcast state to all other clients
							BroadcastPlayerState(id, hits, isFlagHolder, score, stamina, animationState);
							break;
							
						case 4: // FlagUpdate
							bool isPickup = reader.ReadBoolean();
							float flagX = reader.ReadSingle();
							float flagY = reader.ReadSingle();
							float flagZ = reader.ReadSingle();
							
							// Broadcast flag update to all clients
							BroadcastFlagUpdate(id, isPickup, flagX, flagY, flagZ);
							break;
							
						case 7: // Attack
							float attackX = reader.ReadSingle();
							float attackY = reader.ReadSingle();
							float attackZ = reader.ReadSingle();
							
							// Broadcast attack to all other clients
							BroadcastAttack(id, attackX, attackY, attackZ);
							break;
							
						case 8: // TakeHit
							int targetPlayerId = reader.ReadInt32();
							
							// Broadcast hit to all clients
							BroadcastTakeHit(id, targetPlayerId);
							break;
							
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
						// Expected sizes for different packet types
						switch (data[0])
						{
							case 1: // Welcome
								Console.WriteLine("Expected size: 5 bytes (1 byte type + 4 bytes client ID)");
								break;
							case 2: // PlayerPosition
								Console.WriteLine("Expected size: 25 bytes (1 byte type + 6 floats × 4 bytes each)");
								break;
							case 3: // PlayerState
								Console.WriteLine("Expected size: variable (1 byte type + 4 bytes hits + 1 byte flag + 4 bytes score + 4 bytes stamina + 4 bytes string length + string data)");
								break;
							case 4: // FlagUpdate
								Console.WriteLine("Expected size: 18 bytes (1 byte type + 1 byte pickup + 3 floats × 4 bytes each)");
								break;
							case 7: // Attack
								Console.WriteLine("Expected size: 13 bytes (1 byte type + 3 floats × 4 bytes each)");
								break;
						}
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

			private void SendCallback(IAsyncResult ar)
			{
				try
				{
					if (stream != null)
					{
						stream.EndWrite(ar);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error sending TCP data: {ex}");
				}
			}

			// Broadcasting methods
			private void BroadcastPlayerPosition(int playerId, float x, float y, float z, float rotX, float rotY, float rotZ)
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((byte)2); // PlayerPosition
					writer.Write(playerId);
					writer.Write(x);
					writer.Write(y);
					writer.Write(z);
					writer.Write(rotX);
					writer.Write(rotY);
					writer.Write(rotZ);
					
					byte[] data = stream.ToArray();
					BroadcastToAll(data, playerId);
				}
			}

			private void BroadcastPlayerState(int playerId, int hits, bool isFlagHolder, float score, float stamina, string animationState)
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((byte)3); // PlayerState
					writer.Write(playerId);
					writer.Write(hits);
					writer.Write(isFlagHolder);
					writer.Write(score);
					writer.Write(stamina);
					// Send string length first, then string data
					byte[] stringBytes = Encoding.UTF8.GetBytes(animationState);
					writer.Write(stringBytes.Length);
					writer.Write(stringBytes);
					
					byte[] data = stream.ToArray();
					BroadcastToAll(data, playerId);
				}
			}

			private void BroadcastFlagUpdate(int playerId, bool isPickup, float x, float y, float z)
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((byte)4); // FlagUpdate
					writer.Write(isPickup);
					writer.Write(x);
					writer.Write(y);
					writer.Write(z);
					
					byte[] data = stream.ToArray();
					BroadcastToAll(data);
				}
			}

			private void BroadcastAttack(int attackerId, float x, float y, float z)
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((byte)7); // Attack
					writer.Write(attackerId);
					writer.Write(x);
					writer.Write(y);
					writer.Write(z);
					
					byte[] data = stream.ToArray();
					BroadcastToAll(data, attackerId);
				}
			}

			private void BroadcastTakeHit(int attackerId, int targetPlayerId)
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((byte)8); // TakeHit
					writer.Write(targetPlayerId);
					writer.Write(1); // damage
					
					byte[] data = stream.ToArray();
					BroadcastToAll(data);
				}
			}

			private void BroadcastToAll(byte[] data, int excludeId = -1)
			{
				foreach (var client in Server.clients.Values)
				{
					if (client.id != excludeId && client.tcp.socket != null && client.tcp.socket.Connected)
					{
						client.tcp.SendData(data);
					}
				}
			}
			
			private void BroadcastPlayerJoined()
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((byte)5); // PlayerJoined
					writer.Write(id);
					writer.Write(Server.ConnectedPlayers);
					
					byte[] data = stream.ToArray();
					BroadcastToAll(data, id); // Exclude the current client from the broadcast
				}
			}
			
			public void Disconnect()
			{
				if (socket != null && socket.Connected)
				{
					Server.ConnectedPlayers--;
					Console.WriteLine($"Player {id} disconnected. Total players: {Server.ConnectedPlayers}");
					
					// Broadcast player left
					BroadcastPlayerLeft();
					
					socket.Close();
					socket = null; // Set socket to null so the slot can be reused
					Console.WriteLine($"Socket for player {id} set to null");
				}
				else
				{
					Console.WriteLine($"Player {id} disconnect called but socket was null or not connected");
				}
			}
			
			private void BroadcastPlayerLeft()
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((byte)6); // PlayerLeft
					writer.Write(id);
					writer.Write(Server.ConnectedPlayers);
					
					byte[] data = stream.ToArray();
					BroadcastToAll(data, id);
				}
			}
		}
	}
}
