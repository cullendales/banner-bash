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

		// Timer is initialised in Start(), so mark it nullable to satisfy the nullable analyser
		private static System.Timers.Timer? _scoreTimer;

		private static TcpListener? tcpListener;

		// starts the game server based on port from above and max players
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

			// Start periodic score timer (1 second)
			_scoreTimer = new System.Timers.Timer(1000);
			_scoreTimer.Elapsed += (s,e) => ScoreTick();
			_scoreTimer.AutoReset = true;
			_scoreTimer.Start();
		}
		// occurs when new player attempts to connect to the game and accepts new client then listens for more
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
				// Using null-conditional operator avoids CS8602
				if (clients[i].tcp.socket?.Connected != true)
				{
					Console.WriteLine($"Found available slot {i}. Socket null: {clients[i].tcp.socket == null}, Connected: {clients[i].tcp.socket?.Connected ?? false}");
					
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
		// creates slots for new clients and assigns them an id
		private static void InitializeServerData()
		{
			for (int i = 1; i <= MaxPlayers; i++)
			{
				clients.Add(i, new Client(i));
			}
		}
		// updates score for each player id then displays all those scores for every player
		public static void UpdatePlayerScore(int playerId, float score)
		{
			playerScores[playerId] = score;
			DisplayAllScores();
		}
		// displays all players current scores
		public static void DisplayAllScores()
		{
			Console.WriteLine("\n=== PLAYER SCORES ===");
			foreach (var kvp in playerScores)
			{
				if (clients.TryGetValue(kvp.Key, out var cl) && cl.tcp.socket?.Connected == true)
				{
					Console.WriteLine($"Player {kvp.Key}: {kvp.Value:F1} points");
				}
			}
			Console.WriteLine("====================\n");
		}
		// method to remove a player score - used when they disconnect
		public static void RemovePlayerScore(int playerId)
		{
			if (playerScores.ContainsKey(playerId))
			{
				playerScores.Remove(playerId);
				Console.WriteLine($"Removed score for Player {playerId}");
			}
		}

		private static void ScoreTick()
		{
			if (FlagIsHeld && CurrentFlagHolderId != -1)
			{
				if (!playerScores.ContainsKey(CurrentFlagHolderId))
					playerScores[CurrentFlagHolderId] = 0;

				playerScores[CurrentFlagHolderId] += 1;

				BroadcastPlayerScore(CurrentFlagHolderId, playerScores[CurrentFlagHolderId]);

				if (playerScores[CurrentFlagHolderId] >= 100)
				{
					BroadcastGameWon(CurrentFlagHolderId);
					Console.WriteLine($"Player {CurrentFlagHolderId} wins! Resetting match.");

					FlagIsHeld = false;
					CurrentFlagHolderId = -1;
				}
			}
		}

		private static void BroadcastPlayerScore(int pid, float score)
		{
			using var ms = new System.IO.MemoryStream();
			using var w = new System.IO.BinaryWriter(ms);
			w.Write((byte)12); // PlayerScore
			w.Write(pid);
			w.Write(score);
			byte[] data = ms.ToArray();
			BroadcastToAll(data);
		}

		private static void BroadcastGameWon(int winnerId)
		{
			using var ms = new System.IO.MemoryStream();
			using var w = new System.IO.BinaryWriter(ms);
			w.Write((byte)13); // GameWon
			w.Write(winnerId);
			byte[] data = ms.ToArray();
			BroadcastToAll(data);
		}

		private static void BroadcastToAll(byte[] data)
		{
			foreach (var c in clients.Values)
			{
				if (c.tcp.socket?.Connected == true)
					c.tcp.SendData(data);
			}
		}

	}
}