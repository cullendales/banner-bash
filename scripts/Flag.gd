extends Area3D

@export var float_height := Vector3(0, 2, 0)

var holder: Node = null  # Current player holding the flag

func _ready():
	monitoring = true
	body_entered.connect(_on_body_entered)

func _process(delta: float) -> void:
	if holder:
		# Keep flag floating above the holder’s head
		global_position = holder.global_position + float_height

func _on_body_entered(body: Node) -> void:
	if holder == null and body.has_method("take_flag"):
		holder = body
		body.take_flag()
		print("%s picked up the flag!" % body.name)
