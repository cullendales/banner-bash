using Godot;
using System;

public partial class ConnectionUI : Control
{
	[Export] public Label StatusLabel { get; set; }
	[Export] public ProgressBar ConnectionProgress { get; set; }
	[Export] public Button RetryButton { get; set; }
	
	private Godot.Timer _updateTimer;
	
	public override void _Ready()
	{
		// Setup update timer
		_updateTimer = new Godot.Timer();
		_updateTimer.WaitTime = 0.1f; // Update 10 times per second
		_updateTimer.Timeout += OnUpdateTimer;
		AddChild(_updateTimer);
		_updateTimer.Start();
		
		// Setup retry button
		if (RetryButton != null)
		{
			RetryButton.Pressed += OnRetryPressed;
			RetryButton.Visible = false;
		}
		
		UpdateStatus("Ready to connect");
	}
	
	private void OnUpdateTimer()
	{
		if (Client.Instance != null && Client.Instance.IsServerConnected)
		{
			UpdateStatus("Connected to server");
			if (ConnectionProgress != null)
			{
				ConnectionProgress.Value = 100;
			}
			if (RetryButton != null)
			{
				RetryButton.Visible = false;
			}
		}
		else
		{
			UpdateStatus("Not connected");
			if (ConnectionProgress != null)
			{
				ConnectionProgress.Value = 0;
			}
			if (RetryButton != null)
			{
				RetryButton.Visible = true;
			}
		}
	}
	
	private void OnRetryPressed()
	{
		// This can be connected to a retry button if needed
		UpdateStatus("Retry pressed - implement retry logic");
	}
	
	public void UpdateStatus(string status)
	{
		if (StatusLabel != null)
		{
			StatusLabel.Text = status;
		}
	}
	
	public void ShowConnecting()
	{
		UpdateStatus("Connecting...");
		if (ConnectionProgress != null)
		{
			ConnectionProgress.Value = 50;
		}
	}
	
	public void ShowConnected()
	{
		UpdateStatus("Connected!");
		if (ConnectionProgress != null)
		{
			ConnectionProgress.Value = 100;
		}
	}
	
	public void ShowError(string error)
	{
		UpdateStatus($"Error: {error}");
		if (ConnectionProgress != null)
		{
			ConnectionProgress.Value = 0;
		}
		if (RetryButton != null)
		{
			RetryButton.Visible = true;
		}
	}
} 