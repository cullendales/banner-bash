extends Area3D

var holder = null
var is_being_held = false

func _ready():
	body_entered.connect(_on_body_entered)
	visible = true

func _process(delta):
	if holder and holder.is_flag_holder:
		visible = true
		# How far behind and how high above the player's origin:
		var back_dist: float = -0.5
		var up_dist: float = 0.2
		
		# Build an offset transform
		var offset = Transform3D.IDENTITY
		offset.origin = Vector3(0, up_dist, -back_dist)
		
		# Multiply the player's global transform (rotation + position)
		global_transform = holder.global_transform * offset
	else:
		# Show flag when dropped
		if is_being_held:
			is_being_held = false
			holder = null

# Original version with network synchronization
func _on_body_entered(body):
	if not is_being_held and body.has_method("take_flag") and not body.is_flag_holder:
		body.take_flag()
		holder = body
		is_being_held = true

# Called when flag is dropped locally
func drop_at_position(position: Vector3):
	holder = null
	is_being_held = false
	global_position = position
	visible = true

# Network synchronization methods
func handle_pickup():
	if is_being_held:
		print("Flag already held, skipping pickup.")
		return
	
	var game_manager = get_tree().get_current_scene().get_node_or_null("GameManager")
	if game_manager == null:
		print("GameManager not found in handle_pickup()")
		return
	
	var local_player_name = game_manager.local_player_name
	var player = get_tree().get_current_scene().get_node_or_null(local_player_name)
	
	if player and player.has_method("take_flag"):
		print("Flag: handle_pickup -> calling take_flag on ", player.name)
		player.take_flag()
		holder = player
		is_being_held = true
	else:
		print("Local player not found or missing take_flag method")

func handle_drop(position: Vector3):
	# Handle flag being dropped by another player
	visible = true
	holder = null
	is_being_held = false
	global_position = position
	print("Flag was dropped by another player at: ", position)
