using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace GameServer
{
	class Server
	{
		public static int MaxPlayers { get; private set; }
		public static int Port { get; private set; }
		public static Dictionary<int, Client> clients = new Dictionary<int, Client>();
		public static int ConnectedPlayers { get; set; } = 0;
		public static Dictionary<int, float> playerScores = new Dictionary<int, float>();
		public static bool FlagIsHeld = false;
		public static int CurrentFlagHolderId = -1;

		private static TcpListener? tcpListener;


		public static void Start(int _maxPlayer, int _port)
		{
			MaxPlayers = _maxPlayer;
			Port = _port;

			Console.WriteLine("Starting server...");
			InitializeServerData();

			tcpListener = new TcpListener(IPAddress.Any, Port);
			tcpListener.Start();
			tcpListener.BeginAcceptTcpClient(new AsyncCallback(TCPConnectCallback), null);

			Console.WriteLine($"Server started on port {Port}.");
		}

		private static void TCPConnectCallback(IAsyncResult _result)
		{
			if (tcpListener == null) return;
			
			TcpClient _client = tcpListener.EndAcceptTcpClient(_result);
			if (tcpListener != null)
			{
				tcpListener.BeginAcceptTcpClient(new AsyncCallback(TCPConnectCallback), null);
			}
			Console.WriteLine($"Player connecting from {_client.Client?.RemoteEndPoint}");

			for (int i = 1; i <= MaxPlayers; i++)
			{
				// Check if the slot is available (socket is null or not connected)
				if (clients[i].tcp.socket == null || !clients[i].tcp.socket.Connected)
				{
					Console.WriteLine($"Found available slot {i}. Socket null: {clients[i].tcp.socket == null}, Connected: {(clients[i].tcp.socket != null ? clients[i].tcp.socket.Connected : false)}");
					
					// If socket exists but is not connected, clean it up
					if (clients[i].tcp.socket != null)
					{
						Console.WriteLine($"Cleaning up disconnected socket in slot {i}");
						clients[i].tcp.socket.Close();
						clients[i].tcp.socket = null;
					}
					
					Console.WriteLine($"Assigning new client to slot {i}");
					clients[i].tcp.Connect(_client);
					return;
				}
			}

			Console.WriteLine($"{_client.Client?.RemoteEndPoint} failed to connect: server full.");
		}

		private static void InitializeServerData()
		{
			for (int i = 1; i <= MaxPlayers; i++)
			{
				clients.Add(i, new Client(i));
			}
		}

		public static void UpdatePlayerScore(int playerId, float score)
		{
			playerScores[playerId] = score;
			DisplayAllScores();
		}

		public static void DisplayAllScores()
		{
			Console.WriteLine("\n=== PLAYER SCORES ===");
			foreach (var kvp in playerScores)
			{
				if (clients.ContainsKey(kvp.Key) && clients[kvp.Key].tcp.socket != null && clients[kvp.Key].tcp.socket.Connected)
				{
					Console.WriteLine($"Player {kvp.Key}: {kvp.Value:F1} points");
				}
			}
			Console.WriteLine("====================\n");
		}

		public static void RemovePlayerScore(int playerId)
		{
			if (playerScores.ContainsKey(playerId))
			{
				playerScores.Remove(playerId);
				Console.WriteLine($"Removed score for Player {playerId}");
			}
		}


	}
}
