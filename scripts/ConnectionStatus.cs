using Godot;
using System;


// Automatically updates the display every second to show current connection state
// and number of players in the game. Integrates with the Client singleton to
// monitor connection status and player count changes.
public partial class ConnectionStatus : Control
{
	// Label component that displays the current connection status (Connected/Disconnected).
	[Export] public Label StatusLabel { get; set; }
	
	// Label component that displays the current number of players in the game.
	[Export] public Label PlayerCountLabel { get; set; }
	
	// Label component that displays ping information (currently unused but available for future implementation).
	[Export] public Label PingLabel { get; set; }
	
	// Timer used to periodically update the connection status and player count display.
	// Updates every second to provide real-time information.
	private Godot.Timer _updateTimer;
	
	// Internal counter for the current number of players in the game.
	// Updated when receiving player count information from the server.
	private int _playerCount = 0;
	
	// Called when the node is ready. Initializes the update timer and sets initial status.
	// Sets up a timer that fires every second to check and update the connection status.
	public override void _Ready()
	{
		_updateTimer = new Godot.Timer();
		_updateTimer.WaitTime = 1.0f; // Update every second
		_updateTimer.Timeout += OnUpdateTimer;
		AddChild(_updateTimer);
		_updateTimer.Start();
		
		UpdateStatus("Disconnected");
	}
	
	// Timer callback method that checks the current connection status and updates the UI.
	// Called every second to ensure the display reflects the current state.
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
	
	// Updates the status label with the provided status message.
	// Formats the message as "Status: {status}" for display.
	// Parameters:
	//   status - The status message to display
	public void UpdateStatus(string status)
	{
		if (StatusLabel != null)
		{
			StatusLabel.Text = $"Status: {status}";
		}
	}
	
	// Updates the player count display. If a count is provided, it updates the internal counter.
	// If no count is provided (default -1), it displays the current internal count.

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
	
	// Sets the player count and immediately updates the display.
	// This method is called by the Client when receiving player count updates from the server.

	public void SetPlayerCount(int count)
	{
		_playerCount = count;
		UpdatePlayerCount();
	}
} 
