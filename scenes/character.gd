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
@export var input_drop_flag : String = "drop_flag"
@export var input_attack : String = "attack"

## Health/Hit System
@export var max_hits: int = 3
var current_hits: int = 0
var hit_immunity_time: float = 1.0 
var immunity_timer: float = 0.0
var is_immune: bool = false

## Attack System
var attack_cooldown_time: float = 0.5
var attack_cooldown_timer: float = 0.0
var can_attack: bool = true

## Auto-heal System
var auto_heal_time: float = 5.0  
var auto_heal_timer: float = 0.0
var auto_heal_active: bool = false

## PvP Flag Game Variables
var is_flag_holder: bool = false
var is_crouching: bool = false
var score: float = 0.0
var stamina_max = 100
var stamina_current = stamina_max

## Node References
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
	if Input.is_action_just_pressed(input_drop_flag) and is_flag_holder:
		drop_flag()
	if Input.is_action_just_pressed(input_attack) and can_attack:
		perform_attack()

func _physics_process(delta: float) -> void:
	# Handle immunity timer
	if is_immune:
		immunity_timer -= delta
		if immunity_timer <= 0:
			is_immune = false
	
	# Handle attack cooldown
	if not can_attack:
		attack_cooldown_timer -= delta
		if attack_cooldown_timer <= 0:
			can_attack = true
	
	# Handle auto-heal timer
	if current_hits > 0 and auto_heal_active:
		auto_heal_timer -= delta
		if auto_heal_timer <= 0:
			auto_heal()
	
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
			
	# Stamina and movement speed system
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

	# Gain points if holding the flag
	if is_flag_holder:
		score += delta
		if score >= 100:
			print("%s wins!" % name)
			get_tree().paused = true

func perform_attack():
	if not can_attack:
		return
		
	print("%s is attacking!" % name) #Debugging
	can_attack = false
	attack_cooldown_timer = attack_cooldown_time
	
	# Simple attack animation to show hitting
	var tween = create_tween()
	tween.tween_property(self, "scale", Vector3(1.2, 1.2, 1.2), 0.1)
	tween.tween_property(self, "scale", Vector3(1.0, 1.0, 1.0), 0.1)
	
	# Create attack area
	var space_state = get_world_3d().direct_space_state
	var query = PhysicsShapeQueryParameters3D.new()
	
	var attack_shape = SphereShape3D.new()
	attack_shape.radius = 2.0
	query.shape = attack_shape
	
	var attack_position = global_transform
	attack_position.origin += transform.basis.z * -1.5
	query.transform = attack_position
	query.collision_mask = 1
	
	var results = space_state.intersect_shape(query)
	
	for result in results:
		var body = result.collider
		if body != self and body.has_method("take_hit") and body.is_in_group("players"):
			body.take_hit(name)	
			print("⚔️ %s attacked %s!" % [name, body.name])
			break

func take_hit(attacker_name: String = "Unknown"):
	if is_immune:
		return
	
	current_hits += 1
	is_immune = true
	immunity_timer = hit_immunity_time
	
	# Reset auto-heal timer when hit
	auto_heal_timer = auto_heal_time
	auto_heal_active = true
	
	print("%s was hit by %s! (%d/%d hits)" % [name, attacker_name, current_hits, max_hits])
	
	if current_hits >= max_hits:
		if is_flag_holder:
			print("%s took too many hits and dropped the flag!" % name)
			drop_flag()
		
		reset_hits()
		add_knockback(attacker_name)
	
	show_hit_effect()

# Auto heal function, will restore one hit point after 5 seconds of not being hit
func auto_heal():
	if current_hits > 0:
		current_hits -= 1
		auto_heal_active = false
		print("%s auto-healed! (%d/%d hits)" % [name, current_hits, max_hits])
		
		# If still damaged, restart the timer
		if current_hits > 0:
			auto_heal_timer = auto_heal_time
			auto_heal_active = true

func reset_hits():
	current_hits = 0
	is_immune = false
	immunity_timer = 0.0
	auto_heal_active = false

func add_knockback(attacker_name: String):
	# For debugging purpose, ensure that the dummy won't be knockback to test flag dropping mechanics, comment out if needed
	if not can_move:
		print("Character can't move - no knockback applied")
		return
		
	var knockback_force = 5.0
	var knockback_direction = Vector3.ZERO
	
	var attacker = get_tree().get_nodes_in_group("players").filter(func(p): return p.name == attacker_name)
	if attacker.size() > 0:
		knockback_direction = (global_position - attacker[0].global_position).normalized()
	else:
		knockback_direction = Vector3(randf_range(-1, 1), 0, randf_range(-1, 1)).normalized()
	
	velocity += knockback_direction * knockback_force
	
	var original_can_move = can_move
	can_move = false
	await get_tree().create_timer(0.5).timeout
	can_move = original_can_move

# debugging function
func show_hit_effect():
	print("Hit! %d/%d" % [current_hits, max_hits])
	
func rotate_look(rot_input : Vector2):
	look_rotation.x -= rot_input.y * look_speed
	look_rotation.x = clamp(look_rotation.x, deg_to_rad(-85), deg_to_rad(85))
	look_rotation.y -= rot_input.x * look_speed
	
	rotation.y = look_rotation.y
	head.rotation.x = look_rotation.x
	head.rotation.y = 0
	head.rotation.z = 0

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

func toggle_crouch():
	if is_crouching == false:
		scale.y = 0.5
		is_crouching = true
		can_jump = false
		can_sprint = false
	elif is_crouching == true:
		scale.y = 1
		is_crouching = false
		can_jump = true
		can_sprint = true

func take_flag():
	is_flag_holder = true
	flag.holder = self
	reset_hits() 
	print("%s took the flag!" % name)

func drop_flag():
	if not is_flag_holder:
		return
		
	is_flag_holder = false
	var drop_position = global_position + transform.basis.z * -2.0
	drop_position.y = global_position.y + 0.5
	
	flag.drop_at_position(drop_position)
	
	print("%s dropped the flag!" % name)

func force_drop_flag():
	if is_flag_holder:
		drop_flag()

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
			print("%s picked up speed boost!" % name)
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
			print("%s picked up jump boost!" % name)

func check_input_mappings():
	if can_move and not InputMap.has_action(input_left): push_error("Missing input: " + input_left); can_move = false
	if can_move and not InputMap.has_action(input_right): push_error("Missing input: " + input_right); can_move = false
	if can_move and not InputMap.has_action(input_forward): push_error("Missing input: " + input_forward); can_move = false
	if can_move and not InputMap.has_action(input_back): push_error("Missing input: " + input_back); can_move = false
	if can_jump and not InputMap.has_action(input_jump): push_error("Missing input: " + input_jump); can_jump = false
	if can_sprint and not InputMap.has_action(input_sprint): push_error("Missing input: " + input_sprint); can_sprint = false
	if can_freefly and not InputMap.has_action(input_freefly): push_error("Missing input: " + input_freefly); can_freefly = false
	if can_crouch and not InputMap.has_action(input_crouch): push_error("Missing input: " + input_crouch); can_crouch = false
	if not InputMap.has_action(input_drop_flag): push_error("Missing input: " + input_drop_flag)
	if not InputMap.has_action(input_attack): push_error("Missing input: " + input_attack)
