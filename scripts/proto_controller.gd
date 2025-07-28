extends CharacterBody3D

## Movement & Gameplay Settings
@export var can_move : bool = true
@export var has_gravity : bool = true
@export var can_jump : bool = true
@export var can_sprint : bool = true
@export var can_freefly : bool = false
@export var can_crouch : bool = true

@export_group("Speeds")
@export var look_speed : float = 0.002
@export var base_speed : float = 7.0
@export var jump_velocity : float = 4.5
@export var sprint_speed : float = 10.0
@export var freefly_speed : float = 25.0

@export_group("Input Actions")
@export var input_left : String = "move_left"
@export var input_right : String = "move_right"
@export var input_forward : String = "move_forward"
@export var input_back : String = "move_back"
@export var input_jump : String = "jump"
@export var input_sprint : String = "sprint"
@export var input_freefly : String = "freefly"
@export var input_crouch : String = "crouch"

## PvP Flag Game Variables
var is_flag_holder: bool = false
var is_crouching: bool = false
var score: float = 0.0
var run_speed = 1
var stamina_max = 100
var stamina_current = stamina_max
@onready var flag = get_parent().get_node("Flag")
@onready var game = get_tree().get_root().get_node("Map/Game")

## Internals
var mouse_captured : bool = false
var look_rotation : Vector2
var move_speed : float = 0.0
var freeflying : bool = false

@onready var head: Node3D = $Head
@onready var collider: CollisionShape3D = $Collider

func _ready() -> void:
	check_input_mappings()
	look_rotation.y = rotation.y
	look_rotation.x = head.rotation.x
	add_to_group("players")

	# Tagging detection
	if has_node("TagZone"):
		$TagZone.body_entered.connect(_on_tag_zone_body_entered)

func _unhandled_input(event: InputEvent) -> void:
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		capture_mouse()
	if Input.is_key_pressed(KEY_ESCAPE):
		release_mouse()
	if mouse_captured and event is InputEventMouseMotion:
		rotate_look(event.relative)
	if can_freefly and Input.is_action_just_pressed(input_freefly):
		if not freeflying:
			enable_freefly()
		else:
			disable_freefly()
	if can_crouch and Input.is_action_just_pressed(input_crouch):
		toggle_crouch()

func _physics_process(delta: float) -> void:
	if can_freefly and freeflying:
		var input_dir := Input.get_vector(input_left, input_right, input_forward, input_back)
		var motion := (head.global_basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
		motion *= freefly_speed * delta
		move_and_collide(motion)
		return

	if has_gravity and not is_on_floor():
		velocity += get_gravity() * delta

	if can_jump and Input.is_action_just_pressed(input_jump) and is_on_floor():
		velocity.y = jump_velocity

	#move_speed = sprint_speed if can_sprint and Input.is_action_pressed(input_sprint) else base_speed

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

	move_and_slide()

	# Gain points if holding the flag
	if is_flag_holder:
		score += delta
		if score >= 100:
			print("%s wins!" % name)
			get_tree().paused = true
			
	var is_moving := Input.get_vector(input_left, input_right, input_forward, input_back).length() > 0.1
	var sprinting := Input.is_action_pressed(input_sprint) and is_moving and can_sprint and stamina_current > 0 and !is_crouching

	if sprinting:
		stamina_current -= 80 * delta
		move_speed = sprint_speed
		if stamina_current <= 0:
			stamina_current = 0
			can_sprint = false
	else:
		move_speed = base_speed
		stamina_current += 25 * delta
		if stamina_current >= stamina_max:
			stamina_current = stamina_max
			can_sprint = true
	

func rotate_look(rot_input : Vector2):
	look_rotation.x -= rot_input.y * look_speed
	look_rotation.x = clamp(look_rotation.x, deg_to_rad(-85), deg_to_rad(85))
	look_rotation.y -= rot_input.x * look_speed
	transform.basis = Basis()
	rotate_y(look_rotation.y)
	head.transform.basis = Basis()
	head.rotate_x(look_rotation.x)

func enable_freefly():
	collider.disabled = true
	freeflying = true
	velocity = Vector3.ZERO

func disable_freefly():
	collider.disabled = false
	freeflying = false

func capture_mouse():
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
	mouse_captured = true

func release_mouse():
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
	mouse_captured = false

func check_input_mappings():
	if can_move and not InputMap.has_action(input_left): push_error("Missing input: " + input_left); can_move = false
	if can_move and not InputMap.has_action(input_right): push_error("Missing input: " + input_right); can_move = false
	if can_move and not InputMap.has_action(input_forward): push_error("Missing input: " + input_forward); can_move = false
	if can_move and not InputMap.has_action(input_back): push_error("Missing input: " + input_back); can_move = false
	if can_jump and not InputMap.has_action(input_jump): push_error("Missing input: " + input_jump); can_jump = false
	if can_sprint and not InputMap.has_action(input_sprint): push_error("Missing input: " + input_sprint); can_sprint = false
	if can_freefly and not InputMap.has_action(input_freefly): push_error("Missing input: " + input_freefly); can_freefly = false
	if can_crouch and not InputMap.has_action(input_crouch): push_error("Missing input: " + input_crouch); can_crouch = false

func _on_tag_zone_body_entered(body: Node) -> void:
	if body.has_method("is_flag_holder") and body.is_flag_holder:
		body.is_flag_holder = false
		is_flag_holder = true
		flag.holder = self
		print("%s stole the flag from %s!" % [name, body.name])
		
func take_flag():
	is_flag_holder = true
	flag.holder = self
	print("%s took the flag!" % name)

func apply_powerup(type: String, duration: float):
	match type:
		"speed":
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
			jump_velocity *= 2
			var timer = Timer.new()
			timer.one_shot = true
			timer.timeout.connect(func(_timer=timer):
				jump_velocity /= 2
				_timer.queue_free()
			)
			add_child(timer)
			timer.start(duration)
 			
			
	
func toggle_crouch():
	if is_crouching == false:
		scale.y = 0.5
		is_crouching = true
		can_jump = false
		can_sprint = false
		#can_move = false
		#velocity.x = 0
	elif is_crouching == true:
		scale.y = 1
		is_crouching = false
		can_jump = true
		can_sprint = true
		#can_move = true
	
	
