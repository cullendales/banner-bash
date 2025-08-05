extends Area3D

var holder = null
var is_being_held = false

func _ready():
	body_entered.connect(_on_body_entered)
	visible = true

func _process(delta):
	# Follow holder if someone has it
	if holder and holder.is_flag_holder and is_being_held:
		visible = true  # Keep flag visible when held
		# Position flag above holder's head - made higher so you can see it
		global_position = holder.global_position + Vector3(0, 3.0, 0)
		if Engine.get_process_frames() % 60 == 0:  # Debug every 60 frames
			print("Flag: Following holder %s at position %s" % [holder.name, global_position])
	elif is_being_held and not holder:
		# Flag is being held but we don't have a holder reference yet
		# Try to find the player who has the flag
		var map = get_tree().get_root().get_node_or_null("Map")
		if map:
			# Check local player first
			var local_player = map.get_node_or_null("Character")
			if local_player and local_player.is_flag_holder:
				holder = local_player
				print("Flag: Found local player as holder")
			else:
				# Check other players by looking for Player nodes
				for child in map.get_children():
					if child.name.begins_with("Player") and child.has_method("is_flag_holder") and child.is_flag_holder:
						holder = child
						print("Flag: Found other player %s as holder" % child.name)
						break
	else:
		# Show flag when dropped
		if is_being_held:
			is_being_held = false
			holder = null
			print("Flag: Released from holder, now visible on ground")

func _on_body_entered(body):
	# Only pickup if flag is not being held and player doesn't already have it
	if not is_being_held and body.has_method("take_flag") and not body.is_flag_holder:
		body.take_flag()
		holder = body
		is_being_held = true
		print("%s picked up the flag!" % body.name)

# Called when flag is dropped locally
func drop_at_position(position: Vector3):
	holder = null
	is_being_held = false
	global_position = position
	visible = true
	print("Flag dropped locally at: ", position)

# Network synchronization methods
func handle_pickup():
	# Handle flag being picked up by another player
	visible = true  # Keep flag visible when held
	is_being_held = true
	# Note: holder will be set by the _process function when it finds a player with is_flag_holder = true
	print("Flag was picked up by another player")

func handle_drop(position: Vector3):
	# Handle flag being dropped by another player
	visible = true
	holder = null
	is_being_held = false
	global_position = position
	print("Flag was dropped by another player at: ", position)
	print("Flag: Now visible on ground at position: ", position)
