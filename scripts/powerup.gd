extends Area3D

@export var powerup_type: String = "speed" # Can be 'speed', 'jump', 'shield', etc.
@export var effect_duration: float = 5.0

func _ready():
	body_entered.connect(_on_body_entered)

func _on_body_entered(body):
	if body.has_method("apply_powerup"):
		body.apply_powerup(powerup_type, effect_duration)
		queue_free()  # Remove the power-up after pickup
