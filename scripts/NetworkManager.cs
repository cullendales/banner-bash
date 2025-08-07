using Godot;
using System;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
	public static NetworkManager Instance { get; private set; }
	
	[Export] public PackedScene PlayerScene { get; set; }
	
	private Dictionary<int, CharacterBody3D> _otherPlayers = new Dictionary<int, CharacterBody3D>();
	private int _myClientId = -1;
	
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
	
	public override void _Ready()
	{
		GD.Print("NetworkManager._Ready called");
		GD.Print($"NetworkManager is now ready at path: {GetPath()}");
	}
	
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
	
	public void HandlePlayerJoined(int playerId, int totalPlayers)
	{
		GD.Print($"Player {playerId} joined. Total players: {totalPlayers}");
		
		// If this is another player (not us), create their character
		if (playerId != _myClientId)
		{
			CreateOtherPlayer(playerId);
		}
	}
	
	public CharacterBody3D GetLocalPlayer()
	{
		// Return the local player character
		return GetNodeOrNull<CharacterBody3D>("../Character");
	}
	
	public int GetMyClientId()
	{
		return _myClientId;
	}
	
	public Dictionary<int, CharacterBody3D> GetOtherPlayers()
	{
		return _otherPlayers;
	}
} 
