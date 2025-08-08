extends Node

# GDScript wrapper for the C# Client class.
# This provides a GDScript-compatible interface to the networking client,
# allowing GDScript code to interact with the C# Client singleton.
# The wrapper handles the communication between GDScript and C# code.

# Reference to the C# Client singleton instance
var _client: Node

func _ready():
	# Wait for the C# Client to be available in the scene tree
	# This ensures the Client singleton has been initialized
	await get_tree().process_frame
	_client = get_node_or_null("/root/Client")
	if _client == null:
		push_error("Client node not found! Make sure Client.cs is added to the scene tree.")

# Check if the client is currently connected to the server
# Returns true if connected, false otherwise
func is_server_connected() -> bool:
	if _client == null:
		return false
	return _client.IsServerConnected

# Send raw byte data to the server
# Only sends if the client exists and is connected
func send_data(data: PackedByteArray) -> void:
	if _client != null and is_server_connected():
		_client.SendData(data)

# Connect to a server at the specified IP address and port
# Delegates to the C# Client's ConnectToServer method
func connect_to_server(ip: String, port: int) -> void:
	if _client != null:
		_client.ConnectToServer(ip, port)

# Disconnect from the current server
# Delegates to the C# Client's DisconnectFromServer method
func disconnect_from_server() -> void:
	if _client != null:
		_client.DisconnectFromServer()

# Get the Client singleton instance
# Returns the C# Client node if it exists, null otherwise
func get_instance() -> Node:
	return get_node_or_null("/root/Client") 