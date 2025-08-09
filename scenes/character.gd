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
var player_id: int = -1


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
var is_first_person: bool = false

## Node References
@onready var flag: Node3D = get_tree().get_root().get_node_or_null("Map/Game/Flag")
@onready var game = get_tree().get_root().get_node("Map/Game")
@onready var anim_tree: AnimationTree = $MeshInstance3D/Player/AnimationTree
@onready var state_playback: AnimationNodeStateMachinePlayback = anim_tree.get("parameters/playback") as AnimationNodeStateMachinePlayback 

## Internals
var mouse_captured : bool = false
var look_rotation : Vector2
var move_speed : float = 0.0
var freeflying : bool = false

@onready var head: Node3D = $Head
@onready var collider: CollisionShape3D = $Collider

func _ready() -> void:
	if can_move:
		$MeshInstance3D.visible = false
	check_input_mappings()
	look_rotation.y = rotation.y
	look_rotation.x = head.rotation.x
	add_to_group("players")
	set_meta("last_sent_position", global_position)

func set_can_move(value: bool) -> void:
	can_move = value
	print("%s can_move set to: %s" % [name, value])

func _unhandled_input(event: InputEvent) -> void:
	if not can_move:
		return
		
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		capture_mouse()
	if Input.is_key_pressed(KEY_ESCAPE):
		release_mouse()
	if mouse_captured and event is InputEventMouseMotion:
		rotate_look(event.relative)
	if can_freefly and Input.is_action_just_pressed(input_freefly):
		if not freeflying: enable_freefly()
		else: disable_freefly()
	if can_crouch and Input.is_action_just_pressed(input_crouch):
		toggle_crouch()
	if Input.is_action_just_pressed(input_drop_flag) and is_flag_holder:
		drop_flag()
	if Input.is_action_just_pressed(input_attack) and can_attack:
		perform_attack()

func _physics_process(delta: float) -> void:
	# Immunity
	if is_immune:
		immunity_timer -= delta
		if immunity_timer <= 0:
			is_immune = false
	
	# Attack cooldown
	if not can_attack:
		attack_cooldown_timer -= delta
		if attack_cooldown_timer <= 0:
			can_attack = true
	
	# Auto-heal
	if current_hits > 0 and auto_heal_active:
		auto_heal_timer -= delta
		if auto_heal_timer <= 0:
			auto_heal()
	
	# Freefly
	if can_freefly and freeflying:
		var input_dir := Input.get_vector(input_left, input_right, input_forward, input_back)
		var motion := (head.global_basis * Vector3(input_dir.x, 0, input_dir.y)).normalized() * freefly_speed * delta
		move_and_collide(motion)
		return

	# Gravity
	if has_gravity and not is_on_floor():
		velocity += get_gravity() * delta
	
	var just_jumped := false
	if can_jump and Input.is_action_just_pressed(input_jump) and is_on_floor():
		velocity.y = jump_velocity
		just_jumped = true

	# Walk/sprint
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
	
	# Network updates (local player only)
	if can_move:
		var client = get_node_or_null("/root/Client")
		if client and client.IsServerConnected:
			# position (every 3 frames, if moving / changed)
			if Engine.get_process_frames() % 3 == 0:
				var is_moving = Input.get_vector(input_left, input_right, input_forward, input_back).length() > 0.1
				var position_changed = global_position.distance_to(get_meta("last_sent_position", global_position)) > 0.1
				if is_moving or position_changed:
					send_position_packet(global_position, rotation)
					set_meta("last_sent_position", global_position)
			# state (every 15 frames)
			if Engine.get_process_frames() % 15 == 0:
				var current_animation = state_playback.get_current_node() if state_playback else "Idle"
				send_state_packet(current_hits, is_flag_holder, score, stamina_current, current_animation)
			
	# Stamina + speed
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
	
	var input_dir = Input.get_vector(input_left, input_right, input_forward, input_back)
	var walking = input_dir.length() > 0 and is_on_floor()
	var sprintingForAnimation = walking and can_sprint and Input.is_action_pressed(input_sprint)
	var airborne  = not is_on_floor()

	var target_state := "Idle"
	if just_jumped or airborne:
		target_state = "StandingJump"
	elif sprintingForAnimation:
		target_state = "Running"
	elif walking:
		target_state = "Walking"
	if state_playback and state_playback.get_current_node() != target_state:
		state_playback.travel(target_state)

func perform_attack():
	if not can_attack:
		return
	can_attack = false
	attack_cooldown_timer = attack_cooldown_time

	# Tell server about attack if we're the local player
	if can_move:
		var client = get_node_or_null("/root/Client")
		if client and client.IsServerConnected:
			var attack_pos = global_position + transform.basis.z * -1.5
			send_attack_packet(attack_pos)

	# Little visual feedback
	var tw = create_tween()
	tw.tween_property(self, "scale", Vector3(1.2, 1.2, 1.2), 0.1)
	tw.tween_property(self, "scale", Vector3.ONE, 0.1)

	# find nearest player in front within range 
	var forward = -transform.basis.z
	var best_pid = -1
	var best_dist = 999.0
	var ATTACK_RANGE = 2.2  # metres

	for p in get_tree().get_nodes_in_group("players"):
		if p == self:
			continue
		var to_vec = p.global_position - global_position
		var dist = to_vec.length()
		if dist <= ATTACK_RANGE:
			var dot_val = to_vec.normalized().dot(forward)
			if dot_val <= 0.0: # target not roughly in front
				continue
			var pid = -1
			if p.has_method("get_player_id"):
				pid = p.get_player_id()
			if pid != -1 and dist < best_dist:
				best_dist = dist
				best_pid = pid

	if best_pid != -1:
		send_take_hit_packet(best_pid)


func send_take_hit_packet(target_id:int):
	var client = get_node_or_null("/root/Client")
	if client and client.IsServerConnected:
		var p := PackedByteArray()
		p.append(8) # PacketType.TakeHit
		# Write int32 in little-endian so C# BinaryReader reads it correctly
		p.append(target_id & 0xFF)
		p.append((target_id >> 8) & 0xFF)
		p.append((target_id >> 16) & 0xFF)
		p.append((target_id >> 24) & 0xFF)
		client.SendData(p)

func set_player_id(id:int) -> void:
	player_id = id

func set_flag_holder(v: bool) -> void:
	is_flag_holder = v


func get_player_id() -> int:
	if player_id != -1:
		return player_id
	if name.begins_with("Player"):
		var num := name.substr(6)
		return int(num)
	return -1


func take_hit(attacker_name: String = "Unknown"):
	if is_immune: return
	current_hits += 1
	is_immune = true
	immunity_timer = hit_immunity_time
	auto_heal_timer = auto_heal_time
	auto_heal_active = true
	if current_hits >= max_hits:
		reset_hits()
		add_knockback(attacker_name)
	show_hit_effect()

func auto_heal():
	if current_hits > 0:
		current_hits -= 1
		auto_heal_active = false
		if current_hits > 0:
			auto_heal_timer = auto_heal_time
			auto_heal_active = true

func reset_hits():
	current_hits = 0
	is_immune = false
	immunity_timer = 0.0
	auto_heal_active = false

func add_knockback(attacker_name: String):
	if not can_move: return
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

func show_hit_effect():
	print("Hit! %d/%d" % [current_hits, max_hits])

func rotate_look(rot_input : Vector2):
	look_rotation.x -= rot_input.y * look_speed
	look_rotation.x = clamp(look_rotation.x, deg_to_rad(-85), deg_to_rad(85))
	look_rotation.y -= rot_input.x * look_speed
	rotation.y = look_rotation.y
	head.rotation = Vector3(look_rotation.x, 0, 0)

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
	if not is_crouching:
		scale.y = 0.4
		is_crouching = true
		can_jump = false
		can_sprint = false
	else:
		scale.y = 1
		is_crouching = false
		can_jump = true
		can_sprint = true

func take_flag():
	if flag == null:
		print("Warning: Flag not found, cannot request pickup")
		return
	if can_move:
		var client = get_node_or_null("/root/Client")
		if client and client.IsServerConnected:
			send_flag_pickup_packet()
	print("%s requested flag pickup" % name)

func drop_flag():
	if flag == null:
		print("Warning: Flag not found, cannot request drop")
		return
	var drop_position = global_position + transform.basis.z * -2.0
	drop_position.y = global_position.y + 0.5
	if can_move:
		var client = get_node_or_null("/root/Client")
		if client and client.IsServerConnected:
			send_flag_drop_packet(drop_position)
	print("%s requested flag drop" % name)

func force_drop_flag():
	if is_flag_holder:
		drop_flag()

func apply_powerup(type: String, duration: float):
	match type:
		"speed":
			base_speed *= 3
			var t1 = Timer.new()
			t1.one_shot = true
			t1.timeout.connect(func():
				base_speed /= 3
				t1.queue_free()
			)
			add_child(t1)
			t1.start(duration)
		"jump":
			jump_velocity *= 2
			var t2 = Timer.new()
			t2.one_shot = true
			t2.timeout.connect(func(_timer=t2):
				jump_velocity /= 2
				_timer.queue_free()
			)
			add_child(t2)
			t2.start(duration)

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

func set_network_state(hits: int, flag_holder: bool, player_score: float, stamina: float, anim_state: String):
	current_hits = hits
	is_flag_holder = flag_holder
	score = player_score
	stamina_current = stamina
	if state_playback and state_playback.get_current_node() != anim_state:
		state_playback.travel(anim_state)

# ---- Packet writers ----
func send_position_packet(position: Vector3, rotation: Vector3):
	var client = get_node_or_null("/root/Client")
	if client != null and client.IsServerConnected:
		var packet = PackedByteArray()
		packet.append(2) # PacketType.PlayerPosition
		packet.append_array(float_to_bytes(position.x))
		packet.append_array(float_to_bytes(position.y))
		packet.append_array(float_to_bytes(position.z))
		packet.append_array(float_to_bytes(rotation.x))
		packet.append_array(float_to_bytes(rotation.y))
		packet.append_array(float_to_bytes(rotation.z))
		client.SendData(packet)

func send_state_packet(hits: int, is_flag_holder: bool, score: float, stamina: float, animation_state: String):
	var client = get_node_or_null("/root/Client")
	if client != null and client.IsServerConnected:
		var packet = PackedByteArray()
		packet.append(3) # PacketType.PlayerState
		packet.append_array(int_to_bytes(hits))
		packet.append(1 if is_flag_holder else 0)
		packet.append_array(float_to_bytes(score))
		packet.append_array(float_to_bytes(stamina))
		packet.append_array(int_to_bytes(animation_state.length()))
		packet.append_array(animation_state.to_utf8_buffer())
		client.SendData(packet)

func send_attack_packet(attack_position: Vector3):
	var client = get_node_or_null("/root/Client")
	if client != null and client.IsServerConnected:
		var packet = PackedByteArray()
		packet.append(7) # PacketType.Attack
		packet.append_array(float_to_bytes(attack_position.x))
		packet.append_array(float_to_bytes(attack_position.y))
		packet.append_array(float_to_bytes(attack_position.z))
		client.SendData(packet)

func send_flag_pickup_packet():
	var client = get_node_or_null("/root/Client")
	if client != null and client.IsServerConnected:
		var packet = PackedByteArray()
		packet.append(10) # PacketType.RequestFlagPickup
		var pos = flag.global_position if flag else Vector3.ZERO
		packet.append_array(float_to_bytes(pos.x))
		packet.append_array(float_to_bytes(pos.y))
		packet.append_array(float_to_bytes(pos.z))
		client.SendData(packet)

func send_flag_drop_packet(position: Vector3):
	var client = get_node_or_null("/root/Client")
	if client != null and client.IsServerConnected:
		var packet = PackedByteArray()
		packet.append(11) # PacketType.RequestFlagDrop
		packet.append_array(float_to_bytes(position.x))
		packet.append_array(float_to_bytes(position.y))
		packet.append_array(float_to_bytes(position.z))
		client.SendData(packet)

# ---- Byte helpers ----
func float_to_bytes(value: float) -> PackedByteArray:
	var buffer = StreamPeerBuffer.new()
	buffer.put_float(value)
	buffer.seek(0)
	var bytes = PackedByteArray()
	for i in range(4):
		bytes.append(buffer.get_8())
	return bytes

func int_to_bytes(value: int) -> PackedByteArray:
	var buffer = StreamPeerBuffer.new()
	buffer.put_32(value)
	buffer.seek(0)
	return buffer.get_data(4)
