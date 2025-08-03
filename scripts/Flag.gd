extends Area3D

var holder = null

func _ready():
	body_entered.connect(_on_body_entered)
	visible = true

func _process(delta):
	# Follow holder if someone has it
	if holder and holder.is_flag_holder:
		visible = true  # Keep flag visible when held
		# Position flag above holder's head - made higher so you can see it
		global_position = holder.global_position + Vector3(0, 3.0, 0)
	else:
		visible = true  # Show flag when dropped
		if holder:
			holder = null

func _on_body_entered(body):
	# Only pickup if flag is not being held and player doesn't already have it
	if holder == null and body.has_method("take_flag") and not body.is_flag_holder:
		body.take_flag()
		holder = body
		print("%s picked up the flag!" % body.name)

# Called when flag is dropped
func drop_at_position(position: Vector3):
	holder = null
	global_position = position
	visible = true

# Network synchronization methods
func handle_pickup():
	# Handle flag being picked up by another player
	visible = false
	holder = null
	print("Flag was picked up by another player")

func handle_drop(position: Vector3):
	# Handle flag being dropped by another player
	visible = true
	holder = null
	global_position = position
	print("Flag was dropped by another player at: ", position)
