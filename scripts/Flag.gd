extends Area3D

# The networked flag. Requests pickup on local touch; follows holder when server says so.

var holder: Node3D = null
var is_being_held := false

# pickup block so the dropper doesn't immediately re-pickup 
var _blocked_id := -1
var _pickup_block_until := 0.0  # seconds

func _ready():
	monitoring = true
	monitorable = true
	body_entered.connect(_on_body_entered)
	visible = true

func _process(_delta):
	# Follow holder
	if holder != null:
		visible = true
		var back_dist: float = -0.5
		var up_dist: float = 0.2
		var offset := Transform3D.IDENTITY
		offset.origin = Vector3(0, up_dist, -back_dist)
		global_transform = holder.global_transform * offset
	else:
		is_being_held = false
		visible = true

func _on_body_entered(body: Node):
	if holder != null:
		return

	# Only players can request pickup
	if body != null and body.is_in_group("players") and body.has_method("take_flag"):
		# Block the last dropper for a short window after a drop
		var now := float(Time.get_ticks_msec()) / 1000.0
		var pid := -1
		if body.has_method("get_player_id"):
			pid = body.get_player_id()
		if pid == _blocked_id and now < _pickup_block_until:
			return

		# Only allow the local controller to request
		var can := false
		if body.has_method("get"):
			var v = body.get("can_move")
			if typeof(v) == TYPE_BOOL:
				can = v
		if can:
			body.take_flag()

# --- From server (authoritative) ---
func apply_server_update(holder_id: int, is_pickup: bool, world_pos: Vector3):
	if is_pickup:
		# Clear block once someone has it
		_blocked_id = -1
		_pickup_block_until = 0.0
		var p := _find_player_by_id(holder_id)
		if p == null:
			push_warning("Flag: holder %s not found yet; will attach when available" % holder_id)
			# Defer attach until player spawns
			call_deferred("_attach_later", holder_id)
			return
		_attach_player(p)
	else:
		# Dropped by holder_id → block that id for a moment to avoid instant re-pickup
		_blocked_id = holder_id
		_pickup_block_until = float(Time.get_ticks_msec()) / 1000.0 + 0.6
		_detach_to_world(world_pos)

func _attach_later(holder_id:int):
	var p := _find_player_by_id(holder_id)
	if p != null:
		_attach_player(p)

func _attach_player(p: Node3D):
	holder = p
	is_being_held = true
	var back_dist: float = -0.5
	var up_dist: float = 0.2
	var offset := Transform3D.IDENTITY
	offset.origin = Vector3(0, up_dist, -back_dist)
	global_transform = p.global_transform * offset
	visible = true

func _detach_to_world(pos: Vector3):
	holder = null
	is_being_held = false
	global_position = pos
	visible = true

func _find_player_by_id(pid: int) -> Node3D:
	for n in get_tree().get_nodes_in_group("players"):
		if n.has_method("get_player_id") and n.get_player_id() == pid:
			return n
	return null
