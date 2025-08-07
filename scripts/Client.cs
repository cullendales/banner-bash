using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

public partial class Client : Node          // ← must inherit Node
{
	public static Client Instance { get; private set; }
	
	public enum PacketType
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
		RequestFlagPickup = 10
	}
	
	public void RequestFlagPickup(Vector3 flagPosition)
	{
		if (_stream != null && _socket != null && _socket.Connected)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write((byte)PacketType.RequestFlagPickup);  // Send packet type 10
				writer.Write(flagPosition.X);
				writer.Write(flagPosition.Y);
				writer.Write(flagPosition.Z);

				byte[] data = stream.ToArray();
				SendData(data);
			}
			
			GD.Print($"Requested flag pickup at {flagPosition}");
		}
		else
		{
			GD.PrintErr("Cannot send flag pickup request: not connected to server");
		}
	}

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
	private int _myClientId = -1;
	private List<(int playerId, int totalPlayers)> _pendingPlayerJoinedPackets = new List<(int, int)>();
	
	// Public property for GDScript to check connection status
	public bool IsServerConnected
	{
		get { return _socket != null && _socket.Connected; }
	}
	
	// Public property to get the client ID
	public int MyClientId
	{
		get { return _myClientId; }
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
	
	public void RequestSlot(int slotId)
	{
		if (_stream != null && _socket.Connected)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write((byte)PacketType.SlotRequest);
				writer.Write(slotId);
				
				byte[] data = stream.ToArray();
				SendData(data);
			}
			GD.Print($"Requested slot {slotId}");
		}
		else
		{
			GD.PrintErr("Cannot request slot: not connected to server");
		}
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
			GD.Print("TCP handshake done.");
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
		try
		{
			using (MemoryStream stream = new MemoryStream(data))
			{
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
					_myClientId = clientId;
					GD.Print($"My client ID set to: {clientId}");
					
					// Try to set up NetworkManager with deferred execution
					CallDeferred(nameof(SetupNetworkManagerDeferred), clientId);
					
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
					
					GD.Print($"Received PlayerPosition packet for player {playerId} at ({x}, {y}, {z}) with rotation ({rotX}, {rotY}, {rotZ})");
					GD.Print($"My client ID is: {_myClientId}, updating position for player {playerId}");
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
					
					GD.Print($"Received PlayerState packet for player {statePlayerId}: hits={hits}, flag={isFlagHolder}, score={score}, stamina={stamina}, anim={animationState}");
					// Update other player's state
					UpdatePlayerState(statePlayerId, hits, isFlagHolder, score, stamina, animationState);
					break;
					
				case PacketType.FlagUpdate:
					int flagPlayerId = reader.ReadInt32();
					bool isPickup = reader.ReadBoolean();
					float flagX = reader.ReadSingle();
					float flagY = reader.ReadSingle();
					float flagZ = reader.ReadSingle();
					
					GD.Print($"Received FlagUpdate packet: playerId={flagPlayerId}, isPickup={isPickup}, position=({flagX}, {flagY}, {flagZ})");
					if (isPickup)
					{
						// Flag was picked up by a player
						HandleFlagPickup(flagPlayerId);
					}
					else
					{
						// Flag was dropped at position by a player
						HandleFlagDrop(flagPlayerId, new Vector3(flagX, flagY, flagZ));
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
					GD.Print($"My client ID is: {_myClientId}");
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
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error handling packet: {e}");
			GD.PrintErr($"Packet data length: {data.Length}");
			if (data.Length > 0)
			{
				GD.PrintErr($"First byte: {data[0]}");
			}
		}
	}

	public void SendData(byte[] data)
	{
		if (_stream != null && _socket.Connected)
		{
			
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
	
	private void SetupNetworkManagerDeferred(int clientId)
	{
		GD.Print($"SetupNetworkManagerDeferred called for client {clientId}");
		
		// Try to find NetworkManager
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		if (networkManager == null)
		{
			networkManager = NetworkManager.Instance;
			GD.Print($"Trying NetworkManager.Instance: {networkManager != null}");
		}
		
		if (networkManager != null)
		{
			GD.Print($"Found NetworkManager, setting client ID");
			networkManager.SetMyClientId(clientId);
			
			// Process any pending PlayerJoined packets
			ProcessPendingPlayerJoinedPackets();
		}
		else
		{
			GD.PrintErr("NetworkManager not found! Will retry in 0.1 seconds");
			// Retry after a short delay
			GetTree().CreateTimer(0.1f).Timeout += () => SetupNetworkManagerDeferred(clientId);
		}
	}

	private void OnSend(IAsyncResult ar)
	{
		try
		{
			_stream.EndWrite(ar);
			GD.Print("Send completed successfully");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Send error: {e}");
		}
	}

	// These methods will be implemented to handle game-specific logic
	private void UpdatePlayerPosition(int playerId, Vector3 position, Vector3 rotation)
	{
		GD.Print($"UpdatePlayerPosition called for player {playerId} at {position} with rotation {rotation}");
		GD.Print($"My client ID is: {_myClientId}, updating position for player {playerId}");
		
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(UpdatePlayerPositionDeferred), playerId, position, rotation);
		}
		else
		{
			GD.PrintErr("Not inside tree when updating player position!");
		}
	}
	
	private void UpdatePlayerPositionDeferred(int playerId, Vector3 position, Vector3 rotation)
	{
		GD.Print($"UpdatePlayerPositionDeferred called for player {playerId} at {position} with rotation {rotation}");
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		
		// Fallback: try to get NetworkManager through singleton
		if (networkManager == null)
		{
			networkManager = NetworkManager.Instance;
			GD.Print($"Trying NetworkManager.Instance: {networkManager != null}");
		}
		
		if (networkManager != null)
		{
			GD.Print($"Found NetworkManager, updating player {playerId} position");
			networkManager.UpdatePlayerPosition(playerId, position, rotation);
		}
		else
		{
			GD.PrintErr($"NetworkManager not found at /root/Map/Server for player {playerId}");
			GD.PrintErr($"Current scene tree structure:");
			var root = GetTree().Root;
			PrintSceneTree(root, 0);
		}
	}
	
	private void UpdatePlayerState(int playerId, int hits, bool isFlagHolder, float score, float stamina, string animationState)
	{
		GD.Print($"UpdatePlayerState called for player {playerId}: hits={hits}, flag={isFlagHolder}, score={score}, stamina={stamina}, anim={animationState}");
		
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(UpdatePlayerStateDeferred), playerId, hits, isFlagHolder, score, stamina, animationState);
		}
		else
		{
			GD.PrintErr("Not inside tree when updating player state!");
		}
	}
	
	private void UpdatePlayerStateDeferred(int playerId, int hits, bool isFlagHolder, float score, float stamina, string animationState)
	{
		GD.Print($"UpdatePlayerStateDeferred called for player {playerId}: hits={hits}, flag={isFlagHolder}, score={score}, stamina={stamina}, anim={animationState}");
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		
		// Fallback: try to get NetworkManager through singleton
		if (networkManager == null)
		{
			networkManager = NetworkManager.Instance;
			GD.Print($"Trying NetworkManager.Instance: {networkManager != null}");
		}
		
		if (networkManager != null)
		{
			GD.Print($"Found NetworkManager, updating player {playerId} state");
			networkManager.UpdatePlayerState(playerId, hits, isFlagHolder, score, stamina, animationState);
		}
		else
		{
			GD.PrintErr($"NetworkManager not found at /root/Map/Server for player {playerId}");
		}
	}

	private void HandleFlagPickup(int playerId)
	{
		GD.Print($"HandleFlagPickup called for player {playerId}");
		
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(HandleFlagPickupDeferred), playerId);
		}
		else
		{
			GD.PrintErr("Not inside tree when handling flag pickup!");
		}
	}
	
	private void HandleFlagPickupDeferred(int playerId)
	{
		GD.Print($"HandleFlagPickupDeferred called for player {playerId}");
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		
		// Fallback: try to get NetworkManager through singleton
		if (networkManager == null)
		{
			networkManager = NetworkManager.Instance;
			GD.Print($"Trying NetworkManager.Instance: {networkManager != null}");
		}
		
		if (networkManager != null)
		{
			GD.Print($"Found NetworkManager, handling flag pickup for player {playerId}");
			networkManager.HandleFlagPickup(playerId);
		}
		else
		{
			GD.PrintErr($"NetworkManager not found at /root/Map/Server for flag pickup for player {playerId}");
		}
	}

	private void HandleFlagDrop(int playerId, Vector3 position)
	{
		GD.Print($"HandleFlagDrop called for player {playerId} at {position}");
		
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(HandleFlagDropDeferred), playerId, position);
		}
		else
		{
			GD.PrintErr("Not inside tree when handling flag drop!");
		}
	}
	
	private void HandleFlagDropDeferred(int playerId, Vector3 position)
	{
		GD.Print($"HandleFlagDropDeferred called for player {playerId} at {position}");
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		
		// Fallback: try to get NetworkManager through singleton
		if (networkManager == null)
		{
			networkManager = NetworkManager.Instance;
			GD.Print($"Trying NetworkManager.Instance: {networkManager != null}");
		}
		
		if (networkManager != null)
		{
			GD.Print($"Found NetworkManager, handling flag drop for player {playerId}");
			networkManager.HandleFlagDrop(playerId, position);
		}
		else
		{
			GD.PrintErr($"NetworkManager not found at /root/Map/Server for flag drop for player {playerId}");
		}
	}



	private void HandlePlayerAttack(int attackerId, Vector3 attackPosition)
	{
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		if (networkManager != null)
		{
			networkManager.HandlePlayerAttack(attackerId, attackPosition);
		}
	}

	private void HandlePlayerTakeHit(int playerId, int damage)
	{
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		if (networkManager != null)
		{
			networkManager.HandlePlayerTakeHit(playerId, damage);
		}
	}
	
	private void HandlePlayerJoined(int playerId, int totalPlayers)
	{
		GD.Print($"HandlePlayerJoined called for player {playerId}, total players: {totalPlayers}");
		
		// Check if NetworkManager is ready
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		if (networkManager == null)
		{
			networkManager = NetworkManager.Instance;
		}
		
		if (networkManager == null || _myClientId == -1)
		{
			GD.Print($"NetworkManager not ready or client ID not set, queuing PlayerJoined packet for player {playerId}");
			_pendingPlayerJoinedPackets.Add((playerId, totalPlayers));
			return;
		}
		
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			GD.Print($"Calling HandlePlayerJoinedDeferred for player {playerId}");
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
		GD.Print($"HandlePlayerJoinedDeferred called for player {playerId}, total players: {totalPlayers}");
		GD.Print($"My client ID is: {_myClientId}");
		
		// Handle player joined in NetworkManager
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		GD.Print($"NetworkManager at /root/Map/Server: {networkManager != null}");
		
		if (networkManager == null)
		{
			networkManager = NetworkManager.Instance;
			GD.Print($"NetworkManager.Instance: {networkManager != null}");
		}
		
		if (networkManager != null)
		{
			GD.Print("Found NetworkManager, handling player joined");
			networkManager.HandlePlayerJoined(playerId, totalPlayers);
		}
		else
		{
			GD.PrintErr("NetworkManager not found for player joined event");
		}
		
		// Update connection status if available
		var connectionStatus = GetNodeOrNull<ConnectionStatus>("/root/ConnectionStatus");
		if (connectionStatus != null)
		{
			GD.Print("Found ConnectionStatus, setting player count");
			connectionStatus.SetPlayerCount(totalPlayers);
		}
		else
		{
			GD.Print("ConnectionStatus not found");
		}
	}
	
	private void HandlePlayerLeftDeferred(int playerId, int totalPlayers)
	{
		// Remove player from network manager
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		if (networkManager != null)
		{
			networkManager.RemovePlayer(playerId);
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

	private void PrintSceneTree(Node node, int depth)
	{
		var indent = new string(' ', depth * 2);
		GD.PrintErr($"{indent}{node.Name} ({node.GetType().Name})");
		for (int i = 0; i < node.GetChildCount(); i++)
		{
			PrintSceneTree(node.GetChild(i), depth + 1);
		}
	}
	
	private void ProcessPendingPlayerJoinedPackets()
	{
		GD.Print($"Processing {_pendingPlayerJoinedPackets.Count} pending PlayerJoined packets");
		
		foreach (var packet in _pendingPlayerJoinedPackets)
		{
			GD.Print($"Processing pending packet for player {packet.playerId}");
			HandlePlayerJoinedDeferred(packet.playerId, packet.totalPlayers);
		}
		
		_pendingPlayerJoinedPackets.Clear();
	}
}
