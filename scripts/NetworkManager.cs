using Godot;
using System;
using System.Collections.Generic;

// Central manager for handling network game state and player coordination.
// Manages the creation, updating, and removal of both local and remote players.
// Coordinates with the Client singleton to handle network events and maintain
// game state synchronization between all connected players.
// Implements a singleton pattern to ensure only one NetworkManager exists.
public partial class NetworkManager : Node
{
	// Singleton instance of the NetworkManager class. Ensures only one manager exists per application.
	public static NetworkManager Instance { get; private set; }
	
	// PackedScene reference for creating player instances.
	// Auto-loaded from "res://scenes/Character.tscn" if not set manually.
	[Export] public PackedScene PlayerScene { get; set; }
	
	// Dictionary mapping player IDs to their CharacterBody3D instances for remote players.
	// Local player is managed separately and accessed through GetLocalPlayer().
	private Dictionary<int, CharacterBody3D> _otherPlayers = new Dictionary<int, CharacterBody3D>();
	
	// Unique identifier assigned to this client by the server.
	// Used to distinguish between local and remote player updates.
	private int _myClientId = -1;
	
	// Called when the node enters the scene tree. Initializes the singleton instance.
	// If another instance already exists, this instance will be freed.
	// Auto-loads the PlayerScene if not already set.
	public override void _EnterTree()
	{
		GD.Print($"NetworkManager._EnterTree called. Instance: {Instance}");
		if (Instance != null) { 
			GD.Print("Another NetworkManager instance exists, calling QueueFree()");
			QueueFree(); 
			return; 
		}
		Instance = this;
		GD.Print("NetworkManager instance set");
		GD.Print($"NetworkManager path: {GetPath()}");
		
		// Auto-load PlayerScene if not set
		if (PlayerScene == null)
		{
			PlayerScene = GD.Load<PackedScene>("res://scenes/Character.tscn");
			GD.Print("Auto-loaded Character scene for PlayerScene");
		}
	}
	
	// Called when the node is ready. Logs the NetworkManager's readiness and path.
	public override void _Ready()
	{
		GD.Print("NetworkManager._Ready called");
		GD.Print($"NetworkManager is now ready at path: {GetPath()}");
	}
	
	// Sets the client ID for this NetworkManager and creates the local player.
	// Called by the Client when receiving a welcome packet from the server.
	// Parameters:
	//   clientId - The client ID assigned by the server
	public void SetMyClientId(int clientId)
	{
		GD.Print($"NetworkManager.SetMyClientId called with {clientId}. Current ID: {_myClientId}");
		_myClientId = clientId;
		GD.Print($"My client ID set to: {clientId}");
		
		// Create a player for this client
		CreateLocalPlayer(clientId);
		
		// Broadcast that we joined (this will be handled by the server)
		// The server will send PlayerJoined packets to all clients
	}
	
	// Creates a local player instance for this client.
	// Removes any existing local player before creating a new one.
	// Sets the player's initial position based on client ID and enables movement.
	// Parameters:
	//   clientId - The client ID for which to create the local player
	private void CreateLocalPlayer(int clientId)
	{
		GD.Print($"CreateLocalPlayer called for client {clientId}");
		
		if (PlayerScene == null)
		{
			GD.PrintErr("PlayerScene not set in NetworkManager!");
			return;
		}
		
		// Remove any existing local player
		var existingPlayer = GetNodeOrNull("../Character");
		if (existingPlayer != null)
		{
			GD.Print("Removing existing local player");
			existingPlayer.QueueFree();
		}
		
		// Create new local player
		var newPlayer = PlayerScene.Instantiate<CharacterBody3D>();
		newPlayer.Name = $"Character"; // Keep the same name for compatibility
		
		// Set initial position based on client ID
		var spawnPosition = GetSpawnPosition(clientId);
		newPlayer.GlobalPosition = spawnPosition;
		
		// Enable movement for local player
		if (newPlayer.HasMethod("set_can_move"))
		{
			GD.Print($"Calling set_can_move(true) on local player {newPlayer.Name}");
			newPlayer.Call("set_can_move", true);
		}
		else
		{
			GD.PrintErr($"Local player {newPlayer.Name} does not have set_can_move method!");
		}
		
		// Add to the scene
		GetParent().AddChild(newPlayer);
		
		// Capture mouse for local player
		if (newPlayer.HasMethod("capture_mouse"))
		{
			newPlayer.Call("capture_mouse");
		}
		
		GD.Print($"Created local player for client {clientId} at {spawnPosition}");
	}
	
	// Determines the spawn position for a player based on their client ID.
	// Provides predefined positions for clients 1-8 and random positions for additional clients.
	// Ensures players are spread out across the map to avoid spawning conflicts.
	// Parameters:
	//   clientId - The client ID for which to determine spawn position
	// Returns:
	//   The spawn position as a Vector3
	private Vector3 GetSpawnPosition(int clientId)
	{
		Vector3 spawnPos;
		
		// Define spawn positions for different clients - more spread out
		switch (clientId)
		{
			case 1:
				spawnPos = new Vector3(10.0f, 1.1f, 5.0f); // Top right corner
				break;
			case 2:
				spawnPos = new Vector3(0.0f, 1.1f, 0.0f); // Center of map
				break;
			case 3:
				spawnPos = new Vector3(10.0f, 1.1f, -5.0f); // Bottom right corner
				break;
			case 4:
				spawnPos = new Vector3(-10.0f, 1.1f, -5.0f); // Bottom left corner
				break;
			case 5:
				spawnPos = new Vector3(0.0f, 1.1f, 8.0f); // Top center
				break;
			case 6:
				spawnPos = new Vector3(0.0f, 1.1f, -8.0f); // Bottom center
				break;
			case 7:
				spawnPos = new Vector3(8.0f, 1.1f, 0.0f); // Right center
				break;
			case 8:
				spawnPos = new Vector3(-8.0f, 1.1f, 0.0f); // Left center
				break;
			default:
				// Generate a random position for additional players
				var random = new Random();
				var x = (float)(random.NextDouble() * 20.0 - 10.0); // -10 to 10
				var z = (float)(random.NextDouble() * 16.0 - 8.0);  // -8 to 8
				spawnPos = new Vector3(x, 1.1f, z);
				break;
		}
		
		GD.Print($"Spawn position for client {clientId}: {spawnPos}");
		return spawnPos;
	}
	
	// Updates the position and rotation of a remote player.
	// Creates the player if they don't exist, then updates their position using
	// network interpolation for smooth movement.
	// Parameters:
	//   playerId - ID of the player to update
	//   position - New position of the player
	//   rotation - New rotation of the player
	public void UpdatePlayerPosition(int playerId, Vector3 position, Vector3 rotation)
	{
		if (playerId == _myClientId) return; // Don't update our own player
		
		if (!_otherPlayers.ContainsKey(playerId))
		{
			// Create new player instance
			CreateOtherPlayer(playerId);
		}
		
		var player = _otherPlayers[playerId];
		if (player != null)
		{
			// Use interpolation for smoother movement
			if (player.HasMethod("set_network_position"))
			{
				player.Call("set_network_position", position, rotation);
			}
			else
			{
				// Fallback to direct position setting
				player.GlobalPosition = position;
				player.Rotation = rotation;
			}
		}
	}
	
	// Updates the state of a remote player (health, flag status, score, stamina, animation).
	// Creates the player if they don't exist, then updates their state.
	// Also updates the HUD with the new score information.
	// Parameters:
	//   playerId - ID of the player to update
	//   hits - Number of hits the player has taken
	//   isFlagHolder - Whether the player is holding the flag
	//   score - Player's current score
	//   stamina - Player's current stamina
	//   animationState - Current animation state of the player
	public void UpdatePlayerState(int playerId, int hits, bool isFlagHolder, float score, float stamina, string animationState)
	{
		if (playerId == _myClientId) return; // Don't update our own player
		
		GD.Print($"NetworkManager: UpdatePlayerState for player {playerId}, isFlagHolder={isFlagHolder}");
		
		if (!_otherPlayers.ContainsKey(playerId))
		{
			CreateOtherPlayer(playerId);
		}
		
		var player = _otherPlayers[playerId];
		if (player != null && player.HasMethod("set_network_state"))
		{
			GD.Print($"NetworkManager: Calling set_network_state for player {playerId} with isFlagHolder={isFlagHolder}");
			player.Call("set_network_state", hits, isFlagHolder, score, stamina, animationState);
		}
		
		// Update HUD with the new score
		var hud = GetNodeOrNull<CanvasLayer>("/root/Map/HUD");
		GD.Print($"NetworkManager: HUD found: {hud != null}");
		if (hud != null)
		{
			GD.Print($"NetworkManager: HUD has update_player_score method: {hud.HasMethod("update_player_score")}");
			if (hud.HasMethod("update_player_score"))
			{
				GD.Print($"NetworkManager: Calling update_player_score for player {playerId} with score {score}");
				hud.Call("update_player_score", playerId, score);
			}
		}
		else
		{
			GD.PrintErr("NetworkManager: HUD not found at /root/Map/HUD");
		}
	}
	
	// Handles flag pickup events for a specific player.
	// Clears any existing flag holders before assigning the flag to the new holder.
	// Updates both the player's flag holder status and the flag node's tracking.
	// Parameters:
	//   playerId - ID of the player who picked up the flag
	public void HandleFlagPickup(int playerId)
	{
		GD.Print($"NetworkManager: HandleFlagPickup called for player {playerId}");

		// Clear any existing holder state before assigning (except the new holder)
		ClearAllFlagHoldersExcept(playerId);

		// Find the correct CharacterBody3D that should hold the flag
		CharacterBody3D holder = null;
		if (playerId == _myClientId)
		{
			holder = GetLocalPlayer();
		}
		else if (_otherPlayers.ContainsKey(playerId))
		{
			holder = _otherPlayers[playerId];
		}

		if (holder == null)
		{
			GD.PrintErr($"NetworkManager: Could not find player node for id {playerId} in HandleFlagPickup");
			return;
		}

		// Let the player take the flag (sets is_flag_holder etc.)
		bool alreadyHolding = false;
		try
		{
			alreadyHolding = (bool)holder.Get("is_flag_holder");
		}
		catch (Exception) { }
		if (!alreadyHolding && holder.HasMethod("take_flag"))
		{
			holder.Call("take_flag");
		}

		// Update Flag node to track new holder without relying on local player name hacks
		var flag = GetNodeOrNull("../Flag");
		if (flag == null)
		{
			flag = GetNodeOrNull("../Game/Flag");
		}
		if (flag != null)
		{
			flag.Set("holder", holder);
			flag.Set("is_being_held", true);
		}
	}

	// Clears the flag holder status from all players except the specified keeper.
	// Ensures only one player can hold the flag at a time.
	// Parameters:
	//   keeperId - ID of the player who should keep the flag (or -1 for no keeper)
	private void ClearAllFlagHoldersExcept(int keeperId)
	{
		// Local player
		var localPlayer = GetLocalPlayer();
		if (localPlayer != null && localPlayer.HasMethod("force_drop_flag") && keeperId != _myClientId)
		{
			localPlayer.Call("force_drop_flag");
		}

		// Remote players
		foreach (var kvp in _otherPlayers)
		{
			int pid = kvp.Key;
			var other = kvp.Value;
			if (pid != keeperId && other != null && other.HasMethod("force_drop_flag"))
			{
				other.Call("force_drop_flag");
			}
		}
	}
	
	// Handles flag drop events for a specific player.
	// Updates the flag node to handle the drop at the specified position.
	// Clears all flag holder status to ensure no lingering holders.
	// Parameters:
	//   playerId - ID of the player who dropped the flag
	//   position - Position where the flag was dropped
	public void HandleFlagDrop(int playerId, Vector3 position)
	{
		GD.Print($"NetworkManager: HandleFlagDrop called for player {playerId} at {position}");
		// Handle flag drop by another player
		var flag = GetNodeOrNull("../Flag");
		GD.Print($"NetworkManager: Flag found at ../Flag: {flag != null}");
		if (flag == null)
		{
			flag = GetNodeOrNull("../Game/Flag");
			GD.Print($"NetworkManager: Flag found at ../Game/Flag: {flag != null}");
		}
		if (flag != null && flag.HasMethod("handle_drop"))
		{
			GD.Print("NetworkManager: Calling flag.handle_drop()");
			flag.Call("handle_drop", position);
		}
		else
		{
			GD.PrintErr("NetworkManager: Flag not found or missing handle_drop method");
		}

		// Ensure no lingering holders after drop – nobody should hold the flag now
		ClearAllFlagHoldersExcept(-1);
	}

	// Handles player attack events from remote players.
	// Checks if the local player is within attack range and applies damage if hit.
	// Parameters:
	//   attackerId - ID of the player who attacked
	//   attackPosition - Position where the attack occurred
	public void HandlePlayerAttack(int attackerId, Vector3 attackPosition)
	{
		if (attackerId == _myClientId) return; // Don't handle our own attack
		
		// Handle attack from another player
		GD.Print($"Player {attackerId} attacked at {attackPosition}");
		
		// Check if we're in range of the attack
		var myPlayer = GetNodeOrNull("../Character") as Node3D;
		if (myPlayer != null)
		{
			var distance = myPlayer.GlobalPosition.DistanceTo(attackPosition);
			if (distance < 2.0f) // Attack range
			{
				// We got hit!
				if (myPlayer.HasMethod("take_hit"))
				{
					myPlayer.Call("take_hit", $"Player{attackerId}");
				}
			}
		}
	}
	
	// Handles player take hit events for remote players.
	// Applies damage to the specified remote player if they exist.
	// Parameters:
	//   playerId - ID of the player who took damage
	//   damage - Amount of damage taken
	public void HandlePlayerTakeHit(int playerId, int damage)
	{
		if (playerId == _myClientId) return; // Don't handle our own hit
		
		if (_otherPlayers.ContainsKey(playerId))
		{
			var player = _otherPlayers[playerId];
			if (player != null && player.HasMethod("take_hit"))
			{
				player.Call("take_hit", "Network");
			}
		}
	}
	
	// Creates a remote player instance for the specified player ID.
	// Sets the player's initial position and disables movement (controlled by network).
	// Adds the player to the scene and stores it in the _otherPlayers dictionary.
	// Parameters:
	//   playerId - ID of the player to create
	private void CreateOtherPlayer(int playerId)
	{
		GD.Print($"CreateOtherPlayer called for player {playerId}");
		
		if (PlayerScene == null)
		{
			GD.PrintErr("PlayerScene not set in NetworkManager!");
			return;
		}
		
		// Check if player already exists
		if (_otherPlayers.ContainsKey(playerId))
		{
			GD.Print($"Player {playerId} already exists, skipping creation");
			return;
		}
		
		var newPlayer = PlayerScene.Instantiate<CharacterBody3D>();
		newPlayer.Name = $"Player{playerId}";
		
		// Set initial position based on client ID
		var spawnPosition = GetSpawnPosition(playerId);
		newPlayer.GlobalPosition = spawnPosition;
		
		// Disable movement for other players (they're controlled by network)
		if (newPlayer.HasMethod("set_can_move"))
		{
			GD.Print($"Calling set_can_move(false) on other player {newPlayer.Name}");
			newPlayer.Call("set_can_move", false);
		}
		else
		{
			GD.PrintErr($"Other player {newPlayer.Name} does not have set_can_move method!");
		}
		
		// Add to the scene
		GetParent().AddChild(newPlayer);
		_otherPlayers[playerId] = newPlayer;
		
		GD.Print($"Created other player: {playerId} at {spawnPosition}");
	}
	
	// Removes a remote player from the game.
	// Frees the player node and removes it from the _otherPlayers dictionary.
	// Parameters:
	//   playerId - ID of the player to remove
	public void RemovePlayer(int playerId)
	{
		if (_otherPlayers.ContainsKey(playerId))
		{
			var player = _otherPlayers[playerId];
			if (player != null)
			{
				player.QueueFree();
			}
			_otherPlayers.Remove(playerId);
			GD.Print($"Removed player: {playerId}");
		}
	}
	
	// Removes all remote players from the game.
	// Frees all player nodes and clears the _otherPlayers dictionary.
	public void ClearAllPlayers()
	{
		foreach (var player in _otherPlayers.Values)
		{
			if (player != null)
			{
				player.QueueFree();
			}
		}
		_otherPlayers.Clear();
	}
	
	// Handles player joined events from the server.
	// Creates a remote player instance if the joined player is not the local player.
	// Parameters:
	//   playerId - ID of the player who joined
	//   totalPlayers - Total number of players in the game
	public void HandlePlayerJoined(int playerId, int totalPlayers)
	{
		GD.Print($"Player {playerId} joined. Total players: {totalPlayers}");
		
		// If this is another player (not us), create their character
		if (playerId != _myClientId)
		{
			CreateOtherPlayer(playerId);
		}
	}
	
	// Gets the local player's CharacterBody3D instance.
	// Returns: The local player's CharacterBody3D, or null if not found
	public CharacterBody3D GetLocalPlayer()
	{
		// Return the local player character
		return GetNodeOrNull<CharacterBody3D>("../Character");
	}
	
	// Gets the client ID assigned to this NetworkManager.
	// Returns: The client ID, or -1 if not set
	public int GetMyClientId()
	{
		return _myClientId;
	}
	
	// Gets the dictionary of remote players managed by this NetworkManager.
	// Returns: Dictionary mapping player IDs to their CharacterBody3D instances
	public Dictionary<int, CharacterBody3D> GetOtherPlayers()
	{
		return _otherPlayers;
	}
} 
