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
	}
	
	public void SetMyClientId(int clientId)
	{
		GD.Print($"NetworkManager.SetMyClientId called with {clientId}. Current ID: {_myClientId}");
		_myClientId = clientId;
		GD.Print($"My client ID set to: {clientId}");
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
			player.GlobalPosition = position;
			player.Rotation = rotation;
		}
	}
	
	public void UpdatePlayerState(int playerId, int hits, bool isFlagHolder, float score, float stamina, string animationState)
	{
		if (playerId == _myClientId) return; // Don't update our own player
		
		if (!_otherPlayers.ContainsKey(playerId))
		{
			CreateOtherPlayer(playerId);
		}
		
		var player = _otherPlayers[playerId];
		if (player != null && player.HasMethod("set_network_state"))
		{
			player.Call("set_network_state", hits, isFlagHolder, score, stamina, animationState);
		}
	}
	
	public void HandleFlagPickup()
	{
		// Handle flag pickup by another player
		var flag = GetNodeOrNull("../Flag");
		if (flag != null && flag.HasMethod("handle_pickup"))
		{
			flag.Call("handle_pickup");
		}
	}
	
	public void HandleFlagDrop(Vector3 position)
	{
		// Handle flag drop by another player
		var flag = GetNodeOrNull("../Flag");
		if (flag != null && flag.HasMethod("handle_drop"))
		{
			flag.Call("handle_drop", position);
		}
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
		if (PlayerScene == null)
		{
			GD.PrintErr("PlayerScene not set in NetworkManager!");
			return;
		}
		
		var newPlayer = PlayerScene.Instantiate<CharacterBody3D>();
		newPlayer.Name = $"Player{playerId}";
		
		// Add to the scene
		GetParent().AddChild(newPlayer);
		_otherPlayers[playerId] = newPlayer;
		
		GD.Print($"Created other player: {playerId}");
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
} 
