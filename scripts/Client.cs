using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

// Main client class responsible for handling TCP network communication with the game server.
// Implements a singleton pattern to ensure only one client instance exists throughout the application.
// Manages connection state, packet sending/receiving, and coordinates with NetworkManager for game state updates.
public partial class Client : Node          // ← must inherit Node
{
	// Singleton instance of the Client class. Ensures only one client exists per application.
	public static Client Instance { get; private set; }
	
	// Enumeration of all packet types used for client-server communication.
	// Each packet type corresponds to a specific game event or state update.
	public enum PacketType
	{
		Welcome = 1,           // Server welcome message with client ID assignment
		PlayerPosition = 2,    // Player position and rotation updates
		PlayerState = 3,       // Player health, flag status, score, stamina, animation
		FlagUpdate = 4,        // Flag pickup/drop events
		PlayerJoined = 5,      // New player joined the game
		PlayerLeft = 6,        // Player left the game
		Attack = 7,           // Player attack event
		TakeHit = 8,          // Player taking damage
		SlotRequest = 9,      // Request to join a specific slot
		RequestFlagPickup = 10 // Request to pick up flag
	}
	
	// Sends a flag pickup request to the server for the specified flag position.
	// This method is called when the local player attempts to pick up a flag.
	// Parameters:
	//   flagPosition - The 3D position of the flag to pick up
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

	// Called when the node enters the scene tree. Initializes the singleton instance.
	// If another instance already exists, this instance will be freed.
	public override void _EnterTree()
	{
		if (Instance != null) { QueueFree(); return; }
		Instance = this;
		GD.Print("Client singleton initialized");
	}
	
	// Called when the node exits the scene tree. Cleans up the singleton instance.
	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
			GD.Print("Client singleton destroyed");
		}
	}

	// Server IP address for connection. Defaults to localhost. Better to open command prompt, type ipconfig and use the IP 
	// of the internet adapter you are using. 
	[Export] public string Ip   = "127.0.0.1";
	
	// Server port number for connection. Defaults to 7777. Please make sure to open the port in your router settings.
	[Export] public int    Port = 7777;

	// Size of the receive buffer for network communication.
	private const int BufferSize = 4096;
	
	// TCP client socket for network communication with the server.
	public TcpClient      _socket;
	
	// Network stream for reading and writing data to/from the server.
	private NetworkStream  _stream;
	
	// Buffer for receiving data from the server.
	private readonly byte[] _receiveBuffer = new byte[BufferSize];
	
	// Unique identifier assigned to this client by the server.
	private int _myClientId = -1;
	
	// Queue of pending PlayerJoined packets that arrived before NetworkManager was ready.
	// These are processed once the NetworkManager is initialized.
	private List<(int playerId, int totalPlayers)> _pendingPlayerJoinedPackets = new List<(int, int)>();
	
	// Public property for GDScript to check connection status.
	// Returns true if the client is connected to the server.
	public bool IsServerConnected
	{
		get { return _socket != null && _socket.Connected; }
	}
	
	// Public property to get the client ID assigned by the server.
	public int MyClientId
	{
		get { return _myClientId; }
	}

	// Establishes a TCP connection to the specified server.
	// Closes any existing connection before attempting to connect.
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
	
	// Sends a slot request to the server to join a specific player slot.

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
	
	// Disconnects from the server and cleans up network resources.
	// Called when the connection is lost or when explicitly disconnecting.
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
	
	// Callback method called when the TCP connection attempt completes.
	// Handles successful connections and connection failures.

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

	// Callback method called when data is received from the server.
	// Processes the received data and handles packet parsing.

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
	
	// Main packet handling method that parses incoming data and routes to appropriate handlers.
	// Uses a switch statement to handle different packet types based on the first byte.
	// Parameters:
	//   data - Raw packet data received from the server
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

	// Sends raw byte data to the server using asynchronous writing.
	// Parameters:
	//   data - Byte array containing the data to send
	public void SendData(byte[] data)
	{
		if (_stream != null && _socket.Connected)
		{
			
			_stream.BeginWrite(data, 0, data.Length, OnSend, null);
		}
	}
	
	// Sends a welcome response back to the server after receiving a welcome packet.
	// This confirms the client's receipt of the welcome message and client ID.

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
	
	// Sets up the NetworkManager with the assigned client ID.
	// This method is called deferred to ensure it runs on the main thread.
	// If NetworkManager is not ready, it will retry after a short delay.

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

	// Callback method called when data sending completes.
	// Handles any errors that occur during the send operation.

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

	// Updates the position and rotation of a remote player.
	// This method is called when receiving PlayerPosition packets from the server.
	// Uses deferred execution to ensure thread safety.
	// Parameters:
	//   playerId - ID of the player to update
	//   position - New position of the player
	//   rotation - New rotation of the player
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
	
	// Deferred version of UpdatePlayerPosition that runs on the main thread.
	// Finds the NetworkManager and delegates the position update to it.
	// Parameters:
	//   playerId - ID of the player to update
	//   position - New position of the player
	//   rotation - New rotation of the player
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
	
	// Updates the state of a remote player (health, flag status, score, stamina, animation).
	// This method is called when receiving PlayerState packets from the server.
	// Uses deferred execution to ensure thread safety.
	// Parameters:
	//   playerId - ID of the player to update
	//   hits - Number of hits the player has taken
	//   isFlagHolder - Whether the player is holding the flag
	//   score - Player's current score
	//   stamina - Player's current stamina
	//   animationState - Current animation state of the player
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
	
	// Deferred version of UpdatePlayerState that runs on the main thread.
	// Finds the NetworkManager and delegates the state update to it.
	// Parameters:
	//   playerId - ID of the player to update
	//   hits - Number of hits the player has taken
	//   isFlagHolder - Whether the player is holding the flag
	//   score - Player's current score
	//   stamina - Player's current stamina
	//   animationState - Current animation state of the player
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

	// Handles flag pickup events from the server.
	// This method is called when receiving FlagUpdate packets indicating a flag pickup.
	// Uses deferred execution to ensure thread safety.
	// Parameters:
	//   playerId - ID of the player who picked up the flag
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
	
	// Deferred version of HandleFlagPickup that runs on the main thread.
	// Finds the NetworkManager and delegates the flag pickup handling to it.
	// Parameters:
	//   playerId - ID of the player who picked up the flag
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

	// Handles flag drop events from the server.
	// This method is called when receiving FlagUpdate packets indicating a flag drop.
	// Uses deferred execution to ensure thread safety.
	// Parameters:
	//   playerId - ID of the player who dropped the flag
	//   position - Position where the flag was dropped
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
	
	// Deferred version of HandleFlagDrop that runs on the main thread.
	// Finds the NetworkManager and delegates the flag drop handling to it.
	// Parameters:
	//   playerId - ID of the player who dropped the flag
	//   position - Position where the flag was dropped
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

	// Handles player attack events from the server.
	// Delegates the attack handling to the NetworkManager.
	// Parameters:
	//   attackerId - ID of the player who attacked
	//   attackPosition - Position where the attack occurred
	private void HandlePlayerAttack(int attackerId, Vector3 attackPosition)
	{
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		if (networkManager != null)
		{
			networkManager.HandlePlayerAttack(attackerId, attackPosition);
		}
	}

	// Handles player take hit events from the server.
	// Delegates the take hit handling to the NetworkManager.
	// Parameters:
	//   playerId - ID of the player who took damage
	//   damage - Amount of damage taken
	private void HandlePlayerTakeHit(int playerId, int damage)
	{
		var networkManager = GetNodeOrNull<NetworkManager>("/root/Map/Server");
		if (networkManager != null)
		{
			networkManager.HandlePlayerTakeHit(playerId, damage);
		}
	}
	
	// Handles player joined events from the server.
	// If NetworkManager is not ready, the event is queued for later processing.
	// Parameters:
	//   playerId - ID of the player who joined
	//   totalPlayers - Total number of players in the game
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
	
	// Handles player left events from the server.
	// Uses deferred execution to ensure thread safety.
	// Parameters:
	//   playerId - ID of the player who left
	//   totalPlayers - Total number of players remaining in the game
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
	
	// Deferred version of HandlePlayerJoined that runs on the main thread.
	// Finds the NetworkManager and delegates the player joined handling to it.
	// Also updates the connection status UI with the new player count.
	// Parameters:
	//   playerId - ID of the player who joined
	//   totalPlayers - Total number of players in the game
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
	
	// Deferred version of HandlePlayerLeft that runs on the main thread.
	// Removes the player from the NetworkManager and updates the connection status UI.
	// Parameters:
	//   playerId - ID of the player who left
	//   totalPlayers - Total number of players remaining in the game
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
	
	// Notifies UI components about a connection attempt.
	// Uses deferred execution to ensure thread safety.
	private void NotifyConnectionAttempt()
	{
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(NotifyConnectionAttemptDeferred));
		}
	}
	
	// Notifies UI components about a successful connection.
	// Uses deferred execution to ensure thread safety.
	private void NotifyConnectionSuccess()
	{
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(NotifyConnectionSuccessDeferred));
		}
	}
	
	// Notifies UI components about a failed connection attempt.
	// Uses deferred execution to ensure thread safety.
	// Parameters:
	//   error - Error message describing the connection failure
	private void NotifyConnectionFailed(string error)
	{
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(NotifyConnectionFailedDeferred), error);
		}
	}
	
	// Notifies UI components about a disconnection.
	// Uses deferred execution to ensure thread safety.
	private void NotifyDisconnected()
	{
		// Use call_deferred to ensure this runs on the main thread
		if (IsInsideTree())
		{
			CallDeferred(nameof(NotifyDisconnectedDeferred));
		}
	}
	
	// Deferred notification methods (run on main thread)
	
	// Deferred version of NotifyConnectionAttempt that runs on the main thread.
	// Finds and notifies any ConnectionUI components about the connection attempt.
	private void NotifyConnectionAttemptDeferred()
	{
		// Find and notify any ConnectionUI components
		var connectionUI = GetNodeOrNull<ConnectionUI>("/root/ConnectionUI");
		if (connectionUI != null)
		{
			connectionUI.ShowConnecting();
		}
	}
	
	// Deferred version of NotifyConnectionSuccess that runs on the main thread.
	// Finds and notifies any ConnectionUI components about the successful connection.
	private void NotifyConnectionSuccessDeferred()
	{
		// Find and notify any ConnectionUI components
		var connectionUI = GetNodeOrNull<ConnectionUI>("/root/ConnectionUI");
		if (connectionUI != null)
		{
			connectionUI.ShowConnected();
		}
	}
	
	// Deferred version of NotifyConnectionFailed that runs on the main thread.
	// Finds and notifies any ConnectionUI components about the connection failure.
	// Parameters:
	//   error - Error message describing the connection failure
	private void NotifyConnectionFailedDeferred(string error)
	{
		// Find and notify any ConnectionUI components
		var connectionUI = GetNodeOrNull<ConnectionUI>("/root/ConnectionUI");
		if (connectionUI != null)
		{
			connectionUI.ShowError(error);
		}
	}
	
	// Deferred version of NotifyDisconnected that runs on the main thread.
	// Finds and notifies any ConnectionUI components about the disconnection.
	private void NotifyDisconnectedDeferred()
	{
		// Find and notify any ConnectionUI components
		var connectionUI = GetNodeOrNull<ConnectionUI>("/root/ConnectionUI");
		if (connectionUI != null)
		{
			connectionUI.UpdateStatus("Disconnected");
		}
	}

	// Debug method that prints the entire scene tree structure.
	// Used for troubleshooting when NetworkManager cannot be found.
	// Parameters:
	//   node - Root node to start printing from
	//   depth - Current depth in the tree (for indentation)
	private void PrintSceneTree(Node node, int depth)
	{
		var indent = new string(' ', depth * 2);
		GD.PrintErr($"{indent}{node.Name} ({node.GetType().Name})");
		for (int i = 0; i < node.GetChildCount(); i++)
		{
			PrintSceneTree(node.GetChild(i), depth + 1);
		}
	}
	
	// Processes any pending PlayerJoined packets that were received before NetworkManager was ready.
	// This ensures that no player join events are lost during initialization.
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
