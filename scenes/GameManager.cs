using Godot;
using System;

public partial class GameManager : Control
{
	[Export] public PackedScene GameScene { get; set; }
	[Export] public float ConnectionTimeout { get; set; } = 5.0f;
	
	LineEdit _ip, _port;
	Button   _join;
	Label    _statusLabel;
	Godot.Timer    _connectionTimer;
	bool     _isConnecting = false;

	public override void _Ready()
	{
		_ip   = GetNode<LineEdit>("IP");
		_port = GetNode<LineEdit>("Port");
		_join = GetNode<Button>("Join");
		_statusLabel = GetNodeOrNull<Label>("StatusLabel");

		_join.Pressed += JoinPressed;
		
		// Setup connection timer
		_connectionTimer = new Godot.Timer();
		_connectionTimer.WaitTime = ConnectionTimeout;
		_connectionTimer.OneShot = true;
		_connectionTimer.Timeout += OnConnectionTimeout;
		AddChild(_connectionTimer);
		
		UpdateStatus("Ready to connect");
	}

	private void JoinPressed()
	{
		if (_isConnecting) return; // Prevent multiple connection attempts
		
		string ip = string.IsNullOrWhiteSpace(_ip.Text) ? "127.0.0.1" : _ip.Text;
		int    port = int.TryParse(_port.Text, out var p) ? p : 7777;
		
		_isConnecting = true;
		_join.Disabled = true;
		UpdateStatus("Connecting...");
		
		// Start connection timer
		_connectionTimer.Start();
		
		// Connect to server
		Client.Instance.ConnectToServer(ip, port);
		
		// Start checking connection status
		GetTree().CreateTimer(0.1f).Timeout += CheckConnectionStatus;
	}
	
	private void CheckConnectionStatus()
	{
		if (!_isConnecting) return;
		
		if (Client.Instance != null && Client.Instance.IsServerConnected)
		{
			// Connection successful!
			_isConnecting = false;
			_connectionTimer.Stop();
			UpdateStatus("Connected! Loading game...");
			
			// Wait a moment for any welcome packet, then switch scenes
			GetTree().CreateTimer(0.5f).Timeout += SwitchToGameScene;
		}
		else
		{
			// Still connecting, check again in a moment
			GetTree().CreateTimer(0.1f).Timeout += CheckConnectionStatus;
		}
	}
	
	private void OnConnectionTimeout()
	{
		if (_isConnecting)
		{
			_isConnecting = false;
			_join.Disabled = false;
			UpdateStatus("Connection timeout. Please try again.");
		}
	}
	
	private void SwitchToGameScene()
	{
		if (GameScene != null)
		{
			GetTree().ChangeSceneToPacked(GameScene);
		}
		else
		{
			// Fallback: try to load the map scene
			var mapScene = GD.Load<PackedScene>("res://scenes/map.tscn");
			if (mapScene != null)
			{
				GetTree().ChangeSceneToPacked(mapScene);
			}
			else
			{
				UpdateStatus("Error: Could not load game scene");
				_join.Disabled = false;
			}
		}
	}
	
	private void UpdateStatus(string status)
	{
		if (_statusLabel != null)
		{
			_statusLabel.Text = status;
		}
		GD.Print($"GameManager: {status}");
	}
}
