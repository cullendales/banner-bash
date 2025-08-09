extends CharacterBody3D

# Network-controlled player character that represents other players in the game.
# This is a simplified version of the player controller that receives position
# and state updates from the network and interpolates movement smoothly.
# Network players cannot be controlled directly by the local player.

## Network Player - Simplified version for other players
# Network players cannot move themselves (controlled by server)
@export var can_move : bool = false  # Network players can't move themselves
# Whether gravity affects the character
@export var has_gravity : bool = true
# Network players cannot jump (controlled by server)
@export var can_jump : bool = false
# Network players cannot sprint (controlled by server)
@export var can_sprint : bool = false
# Network players cannot enter freefly mode
@export var can_freefly : bool = false
# Network players cannot crouch (controlled by server)
@export var can_crouch : bool = false

## Health/Hit System
# Maximum number of hits the player can take before being knocked out
@export var max_hits: int = 3
# Current number of hits taken
var current_hits: int = 0
# Duration of immunity after taking a hit (seconds)
var hit_immunity_time: float = 1.0 
# Timer for immunity duration
var immunity_timer: float = 0.0
# Whether the player is currently immune to hits
var is_immune: bool = false

## PvP Flag Game Variables
# Whether this player is currently holding the flag
var is_flag_holder: bool = false
# Current score (updated from network)
var score: float = 0.0
# Maximum stamina value
var stamina_max = 100
# Current stamina value (updated from network)
var stamina_current = stamina_max

## Network Interpolation
# Target position for smooth movement interpolation
var target_position: Vector3 = Vector3.ZERO
# Target rotation for smooth movement interpolation
var target_rotation: Vector3 = Vector3.ZERO
# Time to interpolate over (reduced for smoother movement)
var interpolation_time: float = 0.05  # Time to interpolate over (reduced for smoother movement)
# Timer for interpolation progress
var interpolation_timer: float = 0.0
# Starting position for interpolation
var start_position: Vector3 = Vector3.ZERO
# Starting rotation for interpolation
var start_rotation: Vector3 = Vector3.ZERO
# Whether interpolation is currently active
var is_interpolating: bool = false

## Node References
# Reference to the animation tree for character animations
@onready var anim_tree: AnimationTree = $MeshInstance3D/Player/AnimationTree
# Reference to the animation state machine playback
@onready var state_playback: AnimationNodeStateMachinePlayback = anim_tree.get("parameters/playback") as AnimationNodeStateMachinePlayback 

func _ready() -> void:
	# Add to players group for easy access
	add_to_group("players")

func _physics_process(delta: float) -> void:
	# Handle immunity timer - decrease timer and clear immunity when expired
	if is_immune:
		immunity_timer -= delta
		if immunity_timer <= 0:
			is_immune = false
	
	# Handle position interpolation for smooth movement
	if is_interpolating:
		interpolation_timer += delta
		var t = interpolation_timer / interpolation_time
		
		if t >= 1.0:
			# Interpolation complete - set to exact target position/rotation
			global_position = target_position
			rotation = target_rotation
			is_interpolating = false
		else:
			# Use smooth easing for more natural movement
			var eased_t = smoothstep(0.0, 1.0, t)
			global_position = start_position.lerp(target_position, eased_t)
			rotation = start_rotation.lerp(target_rotation, eased_t)
	
	# Apply gravity if enabled and not on floor
	if has_gravity and not is_on_floor():
		velocity += get_custom_gravity() * delta
	
	# Apply movement
	move_and_slide()

# Get custom gravity vector for the character
func get_custom_gravity() -> Vector3:
	return Vector3(0, -9.8, 0)

# Network state synchronization method - called by NetworkManager
# Updates the player's state based on network data
func set_network_state(hits: int, flag_holder: bool, player_score: float, stamina: float, anim_state: String):
	current_hits = hits
	is_flag_holder = flag_holder
	score = player_score
	stamina_current = stamina
	
	# Update animation state if different from current state
	if state_playback and state_playback.get_current_node() != anim_state:
		state_playback.travel(anim_state)

# Smooth position update with interpolation - called by NetworkManager
# Starts interpolation from current position to new position/rotation
func set_network_position(new_position: Vector3, new_rotation: Vector3):
	# Start interpolation from current position/rotation to target
	start_position = global_position
	start_rotation = rotation
	target_position = new_position
	target_rotation = new_rotation
	interpolation_timer = 0.0
	is_interpolating = true

# Handle taking a hit from another player
func take_hit(attacker_name: String = "Unknown"):
	# Ignore hits if currently immune
	if is_immune:
		return
	
	# Increment hit count and set immunity
	current_hits += 1
	is_immune = true
	immunity_timer = hit_immunity_time
	
	print("%s was hit by %s! (%d/%d hits)" % [name, attacker_name, current_hits, max_hits])
	
	# Check if player has taken too many hits
	if current_hits >= max_hits:
		if is_flag_holder:
			print("%s took too many hits and dropped the flag!" % name)
			# Network players don't handle flag dropping themselves
		
		# Reset hits and add knockback effect
		reset_hits()
		add_knockback(attacker_name)
	
	# Show visual hit effect
	show_hit_effect()

# Reset hit count and immunity
func reset_hits():
	current_hits = 0
	is_immune = false
	immunity_timer = 0.0

# Add knockback effect when player is hit
func add_knockback(attacker_name: String):
	var knockback_force = 5.0
	# Random knockback direction in XZ plane
	var knockback_direction = Vector3(randf_range(-1, 1), 0, randf_range(-1, 1)).normalized()
	
	# Apply knockback velocity
	velocity += knockback_direction * knockback_force

# Show visual hit effect (placeholder for future implementation)
func show_hit_effect():
	print("Hit! %d/%d" % [current_hits, max_hits]) 
