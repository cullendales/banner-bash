using Godot;
using System;
using System.IO;
using System.Net.Sockets;

public partial class Client : Node          // ← must inherit Node
{
	public static Client Instance { get; private set; }

	public override void _EnterTree()
	{
		if (Instance != null) { QueueFree(); return; }
		Instance = this;
		GD.Print("Client singleton initialized");
	}
	
	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
			GD.Print("Client singleton destroyed");
		}
	}

	[Export] public string Ip   = "127.0.0.1";
	[Export] public int    Port = 7777;

	private const int BufferSize = 4096;
	public TcpClient      _socket;
	private NetworkStream  _stream;
	private readonly byte[] _receiveBuffer = new byte[BufferSize];
	
	// Public property for GDScript to check connection status
	public bool IsServerConnected
	{
		get { return _socket != null && _socket.Connected; }
	}

	public void ConnectToServer(string ip, int port)
	{
		Ip = ip; Port = port;

		// Close existing connection if any
		DisconnectFromServer();

		_socket = new TcpClient
		{
			ReceiveBufferSize = BufferSize,
			SendBufferSize    = BufferSize
		};

		GD.Print($"Connecting to {Ip}:{Port}");
		_socket.BeginConnect(Ip, Port, OnConnected, null);
		
		// Notify any UI components about connection attempt
		NotifyConnectionAttempt();
	}
	
	public void DisconnectFromServer()
	{
		if (_socket != null)
		{
			if (_socket.Connected)
			{
				_socket.Close();
			}
			_socket.Dispose();
			_socket = null;
		}
		
		if (_stream != null)
		{
			_stream.Close();
			_stream = null;
		}
		
		GD.Print("Disconnected from server");
		NotifyDisconnected();
	}

	/* ---------------- CALLBACKS ---------------- */
	private void OnConnected(IAsyncResult ar)
	{
		try
		{
			_socket.EndConnect(ar);
			if (!_socket.Connected)
			{
				GD.PrintErr("Failed to connect (socket not connected).");
				NotifyConnectionFailed("Socket not connected");
				return;
			}

			_stream = _socket.GetStream();
			_stream.BeginRead(_receiveBuffer, 0, BufferSize, OnReceive, null);
			GD.Print("✅ TCP handshake done.");
			NotifyConnectionSuccess();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Connect error: {e}");
			NotifyConnectionFailed(e.Message);
		}
	}

	private void OnReceive(IAsyncResult ar)
	{
		try
		{
			int len = _stream.EndRead(ar); // How many bytes arrived
			if (len <= 0) 
			{
				// Connection closed
				GD.Print("Server disconnected");
				DisconnectFromServer();
				return;
			}

			byte[] data = new byte[len];
			Array.Copy(_receiveBuffer, data, len);

			GD.Print($"Received {len} bytes from server");
			// Parse the packet
			HandlePacket(data);

			_stream.BeginRead(_receiveBuffer, 0, BufferSize, OnReceive, null);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Receive error: {e}");
			DisconnectFromServer();
		}
	}

	private void HandlePacket(byte[] data)
	{
		using (MemoryStream stream = new MemoryStream(data))
		using (BinaryReader reader = new BinaryReader(stream))
		{
			PacketType packetType = (PacketType)reader.ReadByte();
			GD.Print($"Handling packet type: {packetType}");
			
			switch (packetType)
			{
				case PacketType.Welcome:
					int clientId = reader.ReadInt32();
					GD.Print($"Welcome! Your client ID is: {clientId}");
					// Store client ID for future use
					if (NetworkManager.Instance != null)
					{
						NetworkManager.Instance.SetMyClientId(clientId);
					}
					// Send welcome response back to server
					SendWelcomeResponse(clientId);
					break;
					
				case PacketType.PlayerPosition:
					int playerId = reader.ReadInt32();
					float x = reader.ReadSingle();
					float y = reader.ReadSingle();
					float z = reader.ReadSingle();
					float rotX = reader.ReadSingle();
					float rotY = reader.ReadSingle();
					float rotZ = reader.ReadSingle();
					
					// Update other player's position
					UpdatePlayerPosition(playerId, new Vector3(x, y, z), new Vector3(rotX, rotY, rotZ));
					break;
					
				case PacketType.PlayerState:
					int statePlayerId = reader.ReadInt32();
					int hits = reader.ReadInt32();
					bool isFlagHolder = reader.ReadBoolean();
					float score = reader.ReadSingle();
					float stamina = reader.ReadSingle();
					int stringLength = reader.ReadInt32();
					byte[] stringBytes = reader.ReadBytes(stringLength);
					string animationState = System.Text.Encoding.UTF8.GetString(stringBytes);
					
					// Update other player's state
					UpdatePlayerState(statePlayerId, hits, isFlagHolder, score, stamina, animationState);
					break;
					
				case PacketType.FlagUpdate:
					bool isPickup = reader.ReadBoolean();
					float flagX = reader.ReadSingle();
					float flagY = reader.ReadSingle();
					float flagZ = reader.ReadSingle();
					
					if (isPickup)
					{
						// Flag was picked up by someone
						HandleFlagPickup();
					}
					else
					{
						// Flag was dropped at position
						HandleFlagDrop(new Vector3(flagX, flagY, flagZ));
					}
					break;
					
				case PacketType.Attack:
					int attackerId = reader.ReadInt32();
					float attackX = reader.ReadSingle();
					float attackY = reader.ReadSingle();
					float attackZ = reader.ReadSingle();
					
					// Handle attack from other player
					HandlePlayerAttack(attackerId, new Vector3(attackX, attackY, attackZ));
					break;
					
				case PacketType.TakeHit:
					int hitPlayerId = reader.ReadInt32();
					int damage = reader.ReadInt32();
					
					// Handle player taking hit
					HandlePlayerTakeHit(hitPlayerId, damage);
					break;
					
				case PacketType.PlayerJoined:
					int joinedPlayerId = reader.ReadInt32();
					int totalPlayers = reader.ReadInt32();
					
					GD.Print($"PlayerJoined packet: Player {joinedPlayerId} joined, total players: {totalPlayers}");
					// Handle player joined
					HandlePlayerJoined(joinedPlayerId, totalPlayers);
					break;
					
				case PacketType.PlayerLeft:
					int leftPlayerId = reader.ReadInt32();
					int remainingPlayers = reader.ReadInt32();
					
					// Handle player left
					HandlePlayerLeft(leftPlayerId, remainingPlayers);
					break;
					
				default:
					GD.Print($"Unknown packet type: {packetType}");
					break;
			}
		}
	}

	public void SendData(byte[] data)
	{
		if (_stream != null && _socket.Connected)
		{
			GD.Print($"Sending packet type {data[0]} with {data.Length} bytes");
			_stream.BeginWrite(data, 0, data.Length, OnSend, null);
		}
	}
	
	private void SendWelcomeResponse(int clientId)
	{
		using (MemoryStream stream = new MemoryStream())
		using (BinaryWriter writer = new BinaryWriter(stream))
		{
			writer.Write((byte)PacketType.Welcome);
			writer.Write(clientId);
			
			byte[] data = stream.ToArray();
			SendData(data);
		}
		GD.Print($"Sent welcome response with client ID: {clientId}");
	}

	private void OnSend(IAsyncResult ar)
	{
		try
		{
			_stream.EndWrite(ar);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Send error: {e}");
		}
	}

	// These methods will be implemented to handle game-specific logic
	private void UpdatePlayerPosition(int playerId, Vector3 position, Vector3 rotation)
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.UpdatePlayerPosition(playerId, position, rotation);
		}
	}

	private void UpdatePlayerState(int playerId, int hits, bool isFlagHolder, float score, float stamina, string animationState)
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.UpdatePlayerState(playerId, hits, isFlagHolder, score, stamina, animationState);
		}
	}

	private void HandleFlagPickup()
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.HandleFlagPickup();
		}
	}

	private void HandleFlagDrop(Vector3 position)
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.HandleFlagDrop(position);
		}
	}

	private void HandlePlayerAttack(int attackerId, Vector3 attackPosition)
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.HandlePlayerAttack(attackerId, attackPosition);
		}
	}

	private void HandlePlayerTakeHit(int playerId, int damage)
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.HandlePlayerTakeHit(playerId, damage);
		}
	}
	
	private void HandlePlayerJoined(int playerId, int totalPlayers)
	{
		GD.Print($"Player {playerId} joined. Total players: {totalPlayers}");
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(HandlePlayerJoinedDeferred), playerId, totalPlayers);
		}
	}
	
	private void HandlePlayerLeft(int playerId, int totalPlayers)
	{
		GD.Print($"Player {playerId} left. Total players: {totalPlayers}");
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(HandlePlayerLeftDeferred), playerId, totalPlayers);
		}
	}
	
	// Deferred player handling methods (run on main thread)
	private void HandlePlayerJoinedDeferred(int playerId, int totalPlayers)
	{
		// Update connection status if available
		var connectionStatus = GetNodeOrNull<ConnectionStatus>("/root/ConnectionStatus");
		if (connectionStatus != null)
		{
			connectionStatus.SetPlayerCount(totalPlayers);
		}
	}
	
	private void HandlePlayerLeftDeferred(int playerId, int totalPlayers)
	{
		// Remove player from network manager
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.RemovePlayer(playerId);
		}
		// Update connection status if available
		var connectionStatus = GetNodeOrNull<ConnectionStatus>("/root/ConnectionStatus");
		if (connectionStatus != null)
		{
			connectionStatus.SetPlayerCount(totalPlayers);
		}
	}
	
	// Connection notification methods
	private void NotifyConnectionAttempt()
	{
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(NotifyConnectionAttemptDeferred));
		}
	}
	
	private void NotifyConnectionSuccess()
	{
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(NotifyConnectionSuccessDeferred));
		}
	}
	
	private void NotifyConnectionFailed(string error)
	{
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(NotifyConnectionFailedDeferred), error);
		}
	}
	
	private void NotifyDisconnected()
	{
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(NotifyDisconnectedDeferred));
		}
	}
	
	// Deferred notification methods (run on main thread)
	private void NotifyConnectionAttemptDeferred()
	{
		// Find and notify any ConnectionUI components
		var connectionUI = GetNodeOrNull<ConnectionUI>("/root/ConnectionUI");
		if (connectionUI != null)
		{
			connectionUI.ShowConnecting();
		}
	}
	
	private void NotifyConnectionSuccessDeferred()
	{
		// Find and notify any ConnectionUI components
		var connectionUI = GetNodeOrNull<ConnectionUI>("/root/ConnectionUI");
		if (connectionUI != null)
		{
			connectionUI.ShowConnected();
		}
	}
	
	private void NotifyConnectionFailedDeferred(string error)
	{
		// Find and notify any ConnectionUI components
		var connectionUI = GetNodeOrNull<ConnectionUI>("/root/ConnectionUI");
		if (connectionUI != null)
		{
			connectionUI.ShowError(error);
		}
	}
	
	private void NotifyDisconnectedDeferred()
	{
		// Find and notify any ConnectionUI components
		var connectionUI = GetNodeOrNull<ConnectionUI>("/root/ConnectionUI");
		if (connectionUI != null)
		{
			connectionUI.UpdateStatus("Disconnected");
		}
	}
}
