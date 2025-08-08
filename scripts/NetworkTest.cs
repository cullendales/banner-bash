using Godot;
using System;

// UI component for testing network functionality and debugging network communication.
// Provides buttons to test connection, disconnection, attack events, and flag pickup events.
// Useful for development and debugging network-related issues during game development.
public partial class NetworkTest : Control
{
	// Button component for initiating a connection to the server.
	// Connects to localhost (127.0.0.1) on port 7777 when pressed.
	[Export] public Button ConnectButton { get; set; }
	
	// Button component for disconnecting from the server.
	// Immediately terminates the current connection when pressed.
	[Export] public Button DisconnectButton { get; set; }
	
	// Button component for testing attack functionality.
	// Sends a test attack packet to the server when pressed.
	[Export] public Button TestAttackButton { get; set; }
	
	// Button component for testing flag pickup functionality.
	// Sends a test flag pickup packet to the server when pressed.
	[Export] public Button TestFlagButton { get; set; }
	
	// Label component that displays the current test status and operation results.
	// Shows messages like "Connecting...", "Disconnected", "Sent test attack", etc.
	[Export] public Label StatusLabel { get; set; }
	
	// Called when the node is ready. Sets up button event handlers and initial status.
	// Connects all button press events to their respective handler methods.
	public override void _Ready()
	{
		if (ConnectButton != null)
			ConnectButton.Pressed += OnConnectPressed;
		
		if (DisconnectButton != null)
			DisconnectButton.Pressed += OnDisconnectPressed;
		
		if (TestAttackButton != null)
			TestAttackButton.Pressed += OnTestAttackPressed;
		
		if (TestFlagButton != null)
			TestFlagButton.Pressed += OnTestFlagPressed;
		
		UpdateStatus("Ready to test");
	}
	
	// Event handler for the connect button press.
	// Initiates a connection to the local server (127.0.0.1:7777).
	// Updates the status to show the connection attempt.
	private void OnConnectPressed()
	{
		if (Client.Instance != null)
		{
			Client.Instance.ConnectToServer("127.0.0.1", 7777);
			UpdateStatus("Connecting...");
		}
	}
	
	// Event handler for the disconnect button press.
	// Terminates the current connection to the server.
	// Updates the status to show the disconnection.
	private void OnDisconnectPressed()
	{
		if (Client.Instance != null)
		{
			Client.Instance.DisconnectFromServer();
			UpdateStatus("Disconnected");
		}
	}
	
	// Event handler for the test attack button press.
	// Sends a test attack packet to the server if connected.
	// The packet contains a simple attack event with default values.
	private void OnTestAttackPressed()
	{
		if (Client.Instance != null && Client.Instance.IsServerConnected)
		{
			// Create a simple test attack packet
			var packet = new byte[] { (byte)PacketType.Attack, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			Client.Instance.SendData(packet);
			UpdateStatus("Sent test attack");
		}
		else
		{
			UpdateStatus("Not connected - cannot send attack");
		}
	}
	
	// Event handler for the test flag button press.
	// Sends a test flag pickup packet to the server if connected.
	// The packet contains a simple flag pickup event with default values.
	private void OnTestFlagPressed()
	{
		if (Client.Instance != null && Client.Instance.IsServerConnected)
		{
			// Create a simple test flag pickup packet
			var packet = new byte[] { (byte)PacketType.FlagUpdate, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			Client.Instance.SendData(packet);
			UpdateStatus("Sent test flag pickup");
		}
		else
		{
			UpdateStatus("Not connected - cannot send flag pickup");
		}
	}
	
	// Updates the status label with the provided status message.
	// Also prints the status to the console for debugging purposes.
	// Parameters:
	//   status - The status message to display
	private void UpdateStatus(string status)
	{
		if (StatusLabel != null)
		{
			StatusLabel.Text = $"Test Status: {status}";
		}
		GD.Print($"Network Test: {status}");
	}
} 