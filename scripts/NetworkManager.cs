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
	public void CreateLocalPlayer(int clientId)
	{
		GD.Print($"NetworkManager: Creating local player for client ID {clientId}");

		// Prefer the exported scene; fallback to the same path you used in _EnterTree
		var scene = PlayerScene ?? GD.Load<PackedScene>("res://scenes/Character.tscn");
		if (scene == null)
		{
			GD.PrintErr("CreateLocalPlayer: PlayerScene is null and fallback load failed.");
			return;
		}

		var newPlayer = scene.Instantiate<CharacterBody3D>();
		newPlayer.Name = "Character";

		// Spawn position based on id so both clients don’t overlap
		newPlayer.GlobalPosition = GetSpawnPosition(clientId);

		if (newPlayer.HasMethod("set_player_id"))
			newPlayer.Call("set_player_id", clientId);
		else
			newPlayer.Set("player_id", clientId);

		// Local player can move
		if (newPlayer.HasMethod("set_can_move"))
			newPlayer.Call("set_can_move", true);

		var gameNode = GetNodeOrNull<Node3D>("/root/Map/Game");
		if (gameNode != null) gameNode.AddChild(newPlayer);
		else GetParent().AddChild(newPlayer);

		// Capture mouse if the script exposes it
		if (newPlayer.HasMethod("capture_mouse"))
			newPlayer.Call("capture_mouse");

		// Track my id
		_myClientId = clientId;

		if (!_otherPlayers.ContainsKey(clientId))
			_otherPlayers[clientId] = newPlayer;

		GD.Print("NetworkManager: Local player created and assigned player_id");
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
	}

	public void UpdatePlayerScore(int playerId,float score)
	{
		// Update node property so any local UI bound to it sees the change
		if (playerId == _myClientId)
		{
			var me = GetLocalPlayer();
			if (me != null) me.Set("score", score);
		}
		else if (_otherPlayers.TryGetValue(playerId, out var other) && other != null)
		{
			other.Set("score", score);
		}

		// Update HUD (single source of truth)
		var hud = GetNodeOrNull<CanvasLayer>("/root/Map/HUD");
		if (hud!=null && hud.HasMethod("update_player_score"))
			hud.Call("update_player_score",playerId,score);
	}


	public void HandleGameWon(int winnerId)
	{
		GD.Print($"HandleGameWon: player {winnerId} wins!");
		// Simple: pause game and show label
		GetTree().Paused = true;

		var winLabel = new Label();
		winLabel.Text = winnerId==_myClientId?"You Win!":"Player "+winnerId+" Wins!";
		winLabel.AddThemeColorOverride("font_color",new Color(1,1,0));
		winLabel.Scale = new Vector2(4,4);
		var root = GetTree().Root;
		root.AddChild(winLabel);
		winLabel.AnchorLeft=0.5f; winLabel.AnchorTop=0.5f;
		winLabel.OffsetLeft=-200; winLabel.OffsetTop=-50;
	}
	
	// Handles flag pickup events for a specific player.
	// Clears any existing flag holders before assigning the flag to the new holder.
	// Updates both the player's flag holder status and the flag node's tracking.
	// Parameters:
	//   playerId - ID of the player who picked up the flag
	public void HandleFlagPickup(int playerId)
	{
		GD.Print($"NetworkManager: HandleFlagPickup called for player {playerId}");

		CharacterBody3D holder = null;
		if (playerId == _myClientId) holder = GetLocalPlayer();
		else if (_otherPlayers.ContainsKey(playerId)) holder = _otherPlayers[playerId];

		if (holder == null)
		{
			GD.PrintErr($"NetworkManager: Could not find player node for id {playerId} in HandleFlagPickup");
			return;
		}

		// Clear locals
		ClearAllFlagHoldersExcept(playerId);

		// Mark this player as holder locally
		if (holder.HasMethod("set_flag_holder")) holder.Call("set_flag_holder", true);
		else holder.Set("is_flag_holder", true);

		// Update Flag bookkeeping
		var flag = GetNodeOrNull("../Flag") ?? GetNodeOrNull("../Game/Flag");
		if (flag != null)
		{
			flag.Set("holder", holder);
			flag.Set("is_being_held", true);

			// Make the GDScript snap behind the holder right now
			flag.CallDeferred("apply_server_update", playerId, true, holder.GlobalPosition);
			GD.Print($"HandleFlagPickup → Set holder on {flag.GetPath()} to {holder.Name}");
		}
		else
		{
			GD.PrintErr("NetworkManager: Flag node not found while handling pickup");
		}
	}


	private void ClearAllFlagHoldersExcept(int keeperId)
	{
		// Local player
		var localPlayer = GetLocalPlayer();
		if (localPlayer != null)
		{
			if (keeperId != _myClientId)
			{
				if (localPlayer.HasMethod("set_flag_holder"))
					localPlayer.Call("set_flag_holder", false);
				else
					localPlayer.Set("is_flag_holder", false);
			}
		}

		// Remote players
		foreach (var kvp in _otherPlayers)
		{
			int pid = kvp.Key;
			var other = kvp.Value;
			if (pid == keeperId || other == null) continue;

			if (other.HasMethod("set_flag_holder"))
				other.Call("set_flag_holder", false);
			else
				other.Set("is_flag_holder", false);
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
	if (playerId == _myClientId)
	{
		var my = GetLocalPlayer();
		if (my != null && my.HasMethod("take_hit"))
		{
			my.Call("take_hit", "Network"); 
		}
		return;
	}

	// Otherwise, apply to remote player if we have them
	if (_otherPlayers.ContainsKey(playerId))
	{
		var other = _otherPlayers[playerId];
		if (other != null && other.HasMethod("take_hit"))
		{
			other.Call("take_hit", "Network");
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
		// Set player_id so Flag.gd can find this player by id
		if (newPlayer.HasMethod("set_player_id"))
			newPlayer.Call("set_player_id", playerId);
		else
			newPlayer.Set("player_id", playerId);

		
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

	public void ApplyFlagUpdate(int holderId, bool isPickup, Vector3 worldPos)
	{
		if (isPickup) HandleFlagPickup(holderId);
		else HandleFlagDrop(holderId, worldPos);
	}

	
	// Gets the local player's CharacterBody3D instance.
	// Returns: The local player's CharacterBody3D, or null if not found
	public CharacterBody3D GetLocalPlayer()
	{
		// Attempt 1: original relative path (Map/Character)
		var player = GetNodeOrNull<CharacterBody3D>("../Character");
		if (player != null) return player;

		// Attempt 2: when Character is a child of the Game node (Map/Game/Character)
		player = GetNodeOrNull<CharacterBody3D>("../Game/Character");
		if (player != null) return player;

		// Attempt 3: search the "players" group for a node whose player_id matches this client
		foreach (var node in GetTree().GetNodesInGroup("players"))
		{
			if (node is CharacterBody3D c && node.HasMethod("get_player_id"))
			{
				int pid = (int)node.Call("get_player_id");
				if (pid == _myClientId)
					return c;
			}
		}

		return null; // Not found
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
