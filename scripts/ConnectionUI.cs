using Godot;
using System;


// Displays connection progress, status messages, and provides retry functionality.
// Updates frequently (10 times per second) to provide responsive user feedback
// during connection attempts and status changes.
public partial class ConnectionUI : Control
{
	// Label component that displays the current connection status message.
	[Export] public Label StatusLabel { get; set; }
	
	// Progress bar component that visually indicates connection progress.
	[Export] public ProgressBar ConnectionProgress { get; set; }
	
	// Button component that allows users to retry connection attempts.
	// Only visible when connection fails or is not established.
	[Export] public Button RetryButton { get; set; }
	
	// Timer used to frequently update the connection status display.
	// Updates 10 times per second to provide responsive user feedback.
	private Godot.Timer _updateTimer;
	
	// Called when the node is ready. Initializes the update timer and retry button.
	// Sets up a high-frequency timer for responsive UI updates and configures
	// the retry button for user interaction.
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
	
	// Timer callback method that checks the current connection status and updates the UI accordingly.
	// Called 10 times per second to provide responsive feedback. Updates status text,
	// progress bar value, and retry button visibility based on connection state.
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
	
	// Updates the status label with the provided status message.
	// Directly sets the text of the StatusLabel component if it exists.
	public void UpdateStatus(string status)
	{
		if (StatusLabel != null)
		{
			StatusLabel.Text = status;
		}
	}

	// Called by the Client when a connection attempt is initiated.
	public void ShowConnecting()
	{
		UpdateStatus("Connecting...");
		if (ConnectionProgress != null)
		{
			ConnectionProgress.Value = 50;
		}
	}
	
	// Shows the "Connected!" status and sets progress bar to 100%.
	// Called by the Client when a successful connection is established.
	public void ShowConnected()
	{
		UpdateStatus("Connected!");
		if (ConnectionProgress != null)
		{
			ConnectionProgress.Value = 100;
		}
	}
	
	// Shows an error status with the provided error message.
	// Sets progress bar to 0% and makes the retry button visible.
	// Called by the Client when a connection attempt fails.
	// Parameters:
	//   error - The error message to display
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