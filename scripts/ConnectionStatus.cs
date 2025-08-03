using Godot;
using System;

public partial class ConnectionStatus : Control
{
	[Export] public Label StatusLabel { get; set; }
	[Export] public Label PlayerCountLabel { get; set; }
	[Export] public Label PingLabel { get; set; }
	
	private Godot.Timer _updateTimer;
	private int _playerCount = 0;
	
	public override void _Ready()
	{
		_updateTimer = new Godot.Timer();
		_updateTimer.WaitTime = 1.0f; // Update every second
		_updateTimer.Timeout += OnUpdateTimer;
		AddChild(_updateTimer);
		_updateTimer.Start();
		
		UpdateStatus("Disconnected");
	}
	
	private void OnUpdateTimer()
	{
		if (Client.Instance != null && Client.Instance.IsServerConnected)
		{
			UpdateStatus("Connected");
			UpdatePlayerCount();
		}
		else
		{
			UpdateStatus("Disconnected");
			UpdatePlayerCount(0);
		}
	}
	
	public void UpdateStatus(string status)
	{
		if (StatusLabel != null)
		{
			StatusLabel.Text = $"Status: {status}";
		}
	}
	
	public void UpdatePlayerCount(int count = -1)
	{
		if (count >= 0)
		{
			_playerCount = count;
		}
		
		if (PlayerCountLabel != null)
		{
			PlayerCountLabel.Text = $"Players: {_playerCount}";
		}
	}
	
	public void SetPlayerCount(int count)
	{
		_playerCount = count;
		UpdatePlayerCount();
	}
} 
