extends Node3D
@export var local_scene  := preload("res://scenes/Character.tscn")
@export var remote_scene := preload("res://scenes/remote_player.tscn")
@onready var flag : Node3D = get_tree().get_current_scene().get_node("Game/Flag")

var me : Node3D

func _ready():
	me = local_scene.instantiate()
	add_child(me)
	me.name = "LocalPlayer"
	Networking.send_hello()       # helper we add next

func _process(_d):
	# spawn / update remote players
	for id in Networking.players.keys():
		if id == Networking.my_id: continue
		var n = "Remote_%d" % id
		var rp = get_node_or_null(n)
		if rp == null:
			rp = remote_scene.instantiate()
			add_child(rp); rp.name = n
		var s = Networking.players[id]
		rp.target_pos   = s.pos
		rp.target_rot_y = s.rot[1]
		rp.flags        = { "is_sprint":s.is_sprint, "is_crouch":s.is_crouch }

	# clean up gone players
	for c in get_children():
		if c.name.begins_with("Remote_"):
			var rid = int(c.name.substr(7))
			if !Networking.players.has(rid): c.queue_free()

	# very simple flag follow
	if Networking.flag.holder == Networking.my_id:
		pass                               # local Flag.gd already follows us
	elif Networking.flag.holder != -1:
		var holder = get_node_or_null("Remote_%d" % Networking.flag.holder)
		if holder and flag:
			flag.global_position = holder.global_position + Vector3.UP * 3
	else:
		if flag:
			flag.global_position = Networking.flag.pos
