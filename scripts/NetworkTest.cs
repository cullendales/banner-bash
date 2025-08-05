using Godot;
using System;

public partial class NetworkTest : Control
{
	[Export] public Button ConnectButton { get; set; }
	[Export] public Button DisconnectButton { get; set; }
	[Export] public Button TestAttackButton { get; set; }
	[Export] public Button TestFlagButton { get; set; }
	[Export] public Label StatusLabel { get; set; }
	
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
	
	private void OnConnectPressed()
	{
		if (Client.Instance != null)
		{
			Client.Instance.ConnectToServer("127.0.0.1", 7777);
			UpdateStatus("Connecting...");
		}
	}
	
	private void OnDisconnectPressed()
	{
		if (Client.Instance != null)
		{
			Client.Instance.DisconnectFromServer();
			UpdateStatus("Disconnected");
		}
	}
	
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
	
	private void OnTestFlagPressed()
	{
		if (Client.Instance != null && Client.Instance.IsServerConnected)
		{
			// Create a simple test flag pickup request packet
			var packet = new byte[] { (byte)PacketType.FlagPickupRequest, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			Client.Instance.SendData(packet);
			UpdateStatus("Sent test flag pickup request");
		}
		else
		{
			UpdateStatus("Not connected - cannot send flag pickup request");
		}
	}
	
	private void UpdateStatus(string status)
	{
		if (StatusLabel != null)
		{
			StatusLabel.Text = $"Test Status: {status}";
		}
		GD.Print($"Network Test: {status}");
	}
} 