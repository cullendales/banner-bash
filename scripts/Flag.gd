extends Area3D

# Flag game object that can be picked up and carried by players.
# Handles both local pickup/drop events and network synchronization.
# The flag follows the holder player with a specific offset when being carried.

# Reference to the player currently holding the flag (null if not held)
var holder = null

# Flag indicating whether the flag is currently being held by a player
var is_being_held = false

func _ready():
	# Connect to body entered signal to detect when players touch the flag
	body_entered.connect(_on_body_entered)
	visible = true

func _process(delta):
	# Update flag position and visibility every frame
	if holder and holder.is_flag_holder:
		# Flag is being held - make it visible and follow the holder
		visible = true
		# How far behind and how high above the player's origin:
		var back_dist: float = -0.5
		var up_dist: float = 0.2
		
		# Build an offset transform for the flag position relative to the holder
		var offset = Transform3D.IDENTITY
		offset.origin = Vector3(0, up_dist, -back_dist)
		
		# Multiply the player's global transform (rotation + position) by the offset
		global_transform = holder.global_transform * offset
	else:
		# Flag is not being held - show it at its current position
		if is_being_held:
			# Clear held state if holder is no longer valid
			is_being_held = false
			holder = null

# Original version with network synchronization
# Called when a body (player) enters the flag's area
func _on_body_entered(body):
	# Only allow pickup if flag is not already held and body can take flags
	if not is_being_held and body.has_method("take_flag") and not body.is_flag_holder:
		body.take_flag()
		holder = body
		is_being_held = true

# Called when flag is dropped locally by the current holder
# Sets the flag to the specified position and clears holder state
func drop_at_position(position: Vector3):
	holder = null
	is_being_held = false
	global_position = position
	visible = true

# Network synchronization methods

# Handles flag pickup events from network (called by NetworkManager)
# Attempts to find the local player and give them the flag
func handle_pickup():
	if is_being_held:
		print("Flag already held, skipping pickup.")
		return
	
	# Try to find the GameManager to get local player information
	var game_manager = get_tree().get_current_scene().get_node_or_null("GameManager")
	if game_manager == null:
		print("GameManager not found in handle_pickup()")
		return
	
	# Get the local player's name from the game manager
	var local_player_name = game_manager.local_player_name
	var player = get_tree().get_current_scene().get_node_or_null(local_player_name)
	
	# If local player exists and can take flags, give them the flag
	if player and player.has_method("take_flag"):
		print("Flag: handle_pickup -> calling take_flag on ", player.name)
		player.take_flag()
		holder = player
		is_being_held = true
	else:
		print("Local player not found or missing take_flag method")

# Handles flag drop events from network (called by NetworkManager)
# Sets the flag to the specified position and clears holder state
func handle_drop(position: Vector3):
	# Handle flag being dropped by another player
	visible = true
	holder = null
	is_being_held = false
	global_position = position
	print("Flag was dropped by another player at: ", position)
