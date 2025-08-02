extends CharacterBody3D

@onready var anim_tree : AnimationTree = $AnimationTree
@onready var state     = anim_tree.get("parameters/playback")

var target_pos   : Vector3
var target_rot_y : float
var flags := { "is_sprint":false, "is_crouch":false }

func _physics_process(delta):
	global_position = global_position.lerp(target_pos, 0.20)
	rotation.y      = lerp_angle(rotation.y, target_rot_y, 0.20)

	# very small animation state-machine
	var airborne = !is_on_floor()
	var walk     = (global_position - target_pos).length() > 0.05
	var next = "Idle"
	if airborne:                next = "StandingJump"
	elif flags.is_crouch:       next = "Crouch_Idle"
	elif flags.is_sprint:       next = "Running"
	elif walk:                  next = "Walking"
	if state.get_current_node() != next:
		state.travel(next)
