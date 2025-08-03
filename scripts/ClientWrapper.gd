extends Node

# GDScript wrapper for the C# Client class
# This provides a GDScript-compatible interface to the networking client

var _client: Node

func _ready():
	# Wait for the C# Client to be available
	await get_tree().process_frame
	_client = get_node_or_null("/root/Client")
	if _client == null:
		push_error("Client node not found! Make sure Client.cs is added to the scene tree.")

func is_connected() -> bool:
	if _client == null:
		return false
	return _client.IsServerConnected

func send_data(data: PackedByteArray) -> void:
	if _client != null and is_connected():
		_client.SendData(data)

func connect_to_server(ip: String, port: int) -> void:
	if _client != null:
		_client.ConnectToServer(ip, port)

func disconnect_from_server() -> void:
	if _client != null:
		_client.DisconnectFromServer()

# Static access method
static func get_instance() -> Node:
	return get_node_or_null("/root/Client") 