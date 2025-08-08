extends CharacterBody3D

# Player character controller that handles movement, input, animations, and game mechanics.
# Supports various movement modes including walking, sprinting, jumping, crouching, and freefly.
# Manages flag holding, scoring, stamina, and powerup effects for the PvP flag game.

## Movement & Gameplay Settings
# Whether the character can move (can be disabled for network-controlled players)
@export var can_move : bool = true
# Whether gravity affects the character
@export var has_gravity : bool = true
# Whether the character can jump
@export var can_jump : bool = true
# Whether the character can sprint
@export var can_sprint : bool = true
# Whether the character can enter freefly mode (for debugging)
@export var can_freefly : bool = false
# Whether the character can crouch
@export var can_crouch : bool = true

@export_group("Speeds")
# Mouse look sensitivity
@export var look_speed : float = 0.002
# Base movement speed when walking
@export var base_speed : float = 7.0
# Vertical velocity when jumping
@export var jump_velocity : float = 4.5
# Movement speed when sprinting
@export var sprint_speed : float = 10.0
# Movement speed when in freefly mode
@export var freefly_speed : float = 25.0
# Height of the head when standing
@export var standing_head_height := 1.6
# Height of the head when crouching
@export var crouched_head_height := 0.8
# Height of the collision shape when standing
@export var standing_collider_height := 1.8
# Height of the collision shape when crouching
@export var crouched_collider_height := 1.0

@export_group("Input Actions")
# Input action names for movement and controls
@export var input_left : String = "move_left"
@export var input_right : String = "move_right"
@export var input_forward : String = "move_forward"
@export var input_back : String = "move_back"
@export var input_jump : String = "jump"
@export var input_sprint : String = "sprint"
@export var input_freefly : String = "freefly"
@export var input_crouch : String = "crouch"

## PvP Flag Game Variables
# Whether this player is currently holding the flag
var is_flag_holder: bool = false
# Whether the player is currently crouching
var is_crouching: bool = false
# Current score (increases while holding the flag)
var score: float = 0.0
# Current run speed multiplier
var run_speed = 1
# Maximum stamina value
var stamina_max = 100
# Current stamina value (decreases while sprinting)
var stamina_current = stamina_max
# Reference to the flag object in the scene
@onready var flag = get_parent().get_node("Flag")
# Reference to the game object
@onready var game = get_tree().get_root().get_node("Map/Game")
# Reference to the animation tree for character animations
@onready var anim_tree: AnimationTree = $MeshInstance3D/Player/AnimationTree
# Reference to the animation state machine playback
@onready var state_playback: AnimationNodeStateMachinePlayback = anim_tree.get("parameters/playback") as AnimationNodeStateMachinePlayback 

## Internals
# Whether the mouse is currently captured for look control
var mouse_captured : bool = false
# Current look rotation angles (x = pitch, y = yaw)
var look_rotation : Vector2
# Current movement speed (varies based on sprinting state)
var move_speed : float = 0.0
# Whether the character is in freefly mode
var freeflying : bool = false
# Original collider height (stored for crouching)
var original_collider_height: float = 0.0
# Original scale Y value (stored for crouching)
var original_scale_y: float = 1.0

# Reference to the head node (for look rotation)
@onready var head: Node3D = $Head
# Reference to the collision shape (for crouching)
@onready var collider: CollisionShape3D = $Collider

func _ready() -> void:
	# Check if all required input mappings exist
	check_input_mappings()
	# Initialize look rotation to current rotation
	look_rotation.y = rotation.y
	look_rotation.x = head.rotation.x
	# Add to players group for easy access
	add_to_group("players")
	
	# Store original scale and collider height for crouching
	original_scale_y = scale.y
	if collider.shape is CapsuleShape3D:
		original_collider_height = (collider.shape as CapsuleShape3D).height

	# Set up tagging detection if TagZone exists
	if has_node("TagZone"):
		$TagZone.body_entered.connect(_on_tag_zone_body_entered)

func _unhandled_input(event: InputEvent) -> void:
	# Capture mouse on left click
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		capture_mouse()
	# Release mouse on escape key
	if Input.is_key_pressed(KEY_ESCAPE):
		release_mouse()
	# Handle mouse look when captured
	if mouse_captured and event is InputEventMouseMotion:
		rotate_look(event.relative)
	# Toggle freefly mode
	if can_freefly and Input.is_action_just_pressed(input_freefly):
		if not freeflying:
			enable_freefly()
		else:
			disable_freefly()
	# Toggle crouch
	if can_crouch and Input.is_action_just_pressed(input_crouch):
		toggle_crouch()

func _physics_process(delta: float) -> void:
	# Handle freefly movement if enabled
	if can_freefly and freeflying:
		var input_dir := Input.get_vector(input_left, input_right, input_forward, input_back)
		var motion := (head.global_basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
		motion *= freefly_speed * delta
		move_and_collide(motion)
		return

	# Apply gravity if enabled and not on floor
	if has_gravity and not is_on_floor():
		velocity += get_gravity() * delta

	# Handle jumping
	var just_jumped = false
	if can_jump and Input.is_action_just_pressed(input_jump) and is_on_floor():
		velocity.y = jump_velocity
		just_jumped = true

	# Handle movement
	if can_move:
		var input_dir := Input.get_vector(input_left, input_right, input_forward, input_back)
		var move_dir := (transform.basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
		if move_dir:
			velocity.x = move_dir.x * move_speed
			velocity.z = move_dir.z * move_speed
		else:
			velocity.x = move_toward(velocity.x, 0, move_speed)
			velocity.z = move_toward(velocity.z, 0, move_speed)
	else:
		velocity.x = 0
		velocity.y = 0

	# Apply movement
	move_and_slide()

	# Handle flag scoring - gain points while holding the flag
	if is_flag_holder:
		score += delta
		if score >= 100:
			print("%s wins!" % name)
			get_tree().paused = true
			
	# Handle sprinting and stamina
	var is_moving := Input.get_vector(input_left, input_right, input_forward, input_back).length() > 0.1
	var sprinting := Input.is_action_pressed(input_sprint) and is_moving and can_sprint and stamina_current > 0 and !is_crouching

	if sprinting:
		# Decrease stamina while sprinting
		stamina_current -= 80 * delta
		move_speed = sprint_speed
		if stamina_current <= 0:
			stamina_current = 0
			can_sprint = false
	else:
		# Regenerate stamina when not sprinting
		move_speed = base_speed
		stamina_current += 25 * delta
		if stamina_current >= stamina_max:
			stamina_current = stamina_max
			can_sprint = true

	# Handle animation states
	var input_dir = Input.get_vector(input_left, input_right, input_forward, input_back)
	var walking = input_dir.length() > 0 and is_on_floor()
	var sprintingForAnimation = walking and can_sprint and Input.is_action_pressed(input_sprint)
	var airborne  = not is_on_floor()

	# Determine target animation state
	var target_state:String
	if just_jumped or airborne:
		target_state = "StandingJump"
	elif sprintingForAnimation:
		target_state = "Running"
	elif walking:
		target_state = "Walking"
	else:
		target_state = "Idle"
	
	# Switch to target state if different from current state
	if state_playback.get_current_node() != target_state:
		state_playback.travel(target_state)

# Rotate the character's look direction based on mouse input
func rotate_look(rot_input : Vector2):
	look_rotation.x -= rot_input.y * look_speed
	look_rotation.x = clamp(look_rotation.x, deg_to_rad(-85), deg_to_rad(85))
	look_rotation.y -= rot_input.x * look_speed
	transform.basis = Basis()
	rotate_y(look_rotation.y)
	head.rotation.x = look_rotation.x

# Enable freefly mode (disables collision and gravity)
func enable_freefly():
	collider.disabled = true
	freeflying = true
	velocity = Vector3.ZERO

# Disable freefly mode (enables collision and gravity)
func disable_freefly():
	collider.disabled = false
	freeflying = false

# Capture the mouse for look control
func capture_mouse():
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
	mouse_captured = true

# Release the mouse from look control
func release_mouse():
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
	mouse_captured = false

# Check if all required input mappings exist
func check_input_mappings():
	if can_move and not InputMap.has_action(input_left): push_error("Missing input: " + input_left); can_move = false
	if can_move and not InputMap.has_action(input_right): push_error("Missing input: " + input_right); can_move = false
	if can_move and not InputMap.has_action(input_forward): push_error("Missing input: " + input_forward); can_move = false
	if can_move and not InputMap.has_action(input_back): push_error("Missing input: " + input_back); can_move = false
	if can_jump and not InputMap.has_action(input_jump): push_error("Missing input: " + input_jump); can_jump = false
	if can_sprint and not InputMap.has_action(input_sprint): push_error("Missing input: " + input_sprint); can_sprint = false
	if can_freefly and not InputMap.has_action(input_freefly): push_error("Missing input: " + input_freefly); can_freefly = false
	if can_crouch and not InputMap.has_action(input_crouch): push_error("Missing input: " + input_crouch); can_crouch = false

# Handle flag stealing when entering another player's tag zone
func _on_tag_zone_body_entered(body: Node) -> void:
	if body.has_method("is_flag_holder") and body.is_flag_holder:
		body.is_flag_holder = false
		is_flag_holder = true
		flag.holder = self
		print("%s stole the flag from %s!" % [name, body.name])
		
# Take the flag (called when picking up the flag)
func take_flag():
	is_flag_holder = true
	flag.holder = self
	print("%s took the flag!" % name)
	

# Apply a powerup effect to the character
func apply_powerup(type: String, duration: float):
	match type:
		"speed":
			# Increase movement speed for the duration
			base_speed *= 3
			var timer = Timer.new()
			timer.one_shot = true
			timer.timeout.connect(func():
				base_speed /= 3
				timer.queue_free()
			)
			add_child(timer)
			timer.start(duration)
		"jump":
			# Increase jump velocity for the duration
			jump_velocity *= 2
			var timer = Timer.new()
			timer.one_shot = true
			timer.timeout.connect(func(_timer=timer):
				jump_velocity /= 2
				_timer.queue_free()
			)
			add_child(timer)
			timer.start(duration)
 				
# Toggle between standing and crouching states
func toggle_crouch():
	var shape := collider.shape as CapsuleShape3D
	if shape == null:
		return

	is_crouching = !is_crouching

	if is_crouching:
		# Crouch down - lower head position and collider height
		head.position.y = crouched_head_height
		shape.height = crouched_collider_height
		can_jump = false
		can_sprint = false
	else:
		# Stand up - restore head position and collider height
		head.position.y = standing_head_height
		shape.height = standing_collider_height
		can_jump = true
		can_sprint = true

		
	
	
