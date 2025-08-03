extends CharacterBody3D

## Network Player - Simplified version for other players
@export var can_move : bool = false  # Network players can't move themselves
@export var has_gravity : bool = true
@export var can_jump : bool = false
@export var can_sprint : bool = false
@export var can_freefly : bool = false
@export var can_crouch : bool = false

## Health/Hit System
@export var max_hits: int = 3
var current_hits: int = 0
var hit_immunity_time: float = 1.0 
var immunity_timer: float = 0.0
var is_immune: bool = false

## PvP Flag Game Variables
var is_flag_holder: bool = false
var score: float = 0.0
var stamina_max = 100
var stamina_current = stamina_max

## Node References
@onready var anim_tree: AnimationTree = $MeshInstance3D/Player/AnimationTree
@onready var state_playback: AnimationNodeStateMachinePlayback = anim_tree.get("parameters/playback") as AnimationNodeStateMachinePlayback 

func _ready() -> void:
	add_to_group("players")

func _physics_process(delta: float) -> void:
	# Handle immunity timer
	if is_immune:
		immunity_timer -= delta
		if immunity_timer <= 0:
			is_immune = false
	
	# Apply gravity
	if has_gravity and not is_on_floor():
		velocity += get_custom_gravity() * delta
	
	move_and_slide()

func get_custom_gravity() -> Vector3:
	return Vector3(0, -9.8, 0)

# Network state synchronization method
func set_network_state(hits: int, flag_holder: bool, player_score: float, stamina: float, anim_state: String):
	current_hits = hits
	is_flag_holder = flag_holder
	score = player_score
	stamina_current = stamina
	
	# Update animation state if different
	if state_playback and state_playback.get_current_node() != anim_state:
		state_playback.travel(anim_state)

func take_hit(attacker_name: String = "Unknown"):
	if is_immune:
		return
	
	current_hits += 1
	is_immune = true
	immunity_timer = hit_immunity_time
	
	print("%s was hit by %s! (%d/%d hits)" % [name, attacker_name, current_hits, max_hits])
	
	if current_hits >= max_hits:
		if is_flag_holder:
			print("%s took too many hits and dropped the flag!" % name)
			# Network players don't handle flag dropping themselves
		
		reset_hits()
		add_knockback(attacker_name)
	
	show_hit_effect()

func reset_hits():
	current_hits = 0
	is_immune = false
	immunity_timer = 0.0

func add_knockback(attacker_name: String):
	var knockback_force = 5.0
	var knockback_direction = Vector3(randf_range(-1, 1), 0, randf_range(-1, 1)).normalized()
	
	velocity += knockback_direction * knockback_force

func show_hit_effect():
	print("Hit! %d/%d" % [current_hits, max_hits]) 