extends Area3D

# Powerup game object that can be collected by players to gain temporary abilities.
# When a player touches the powerup, it applies the specified effect and then disappears.
# Supports different types of powerups like speed boost, jump boost, shield, etc.

# Type of powerup effect to apply when collected (e.g., 'speed', 'jump', 'shield')
@export var powerup_type: String = "speed" # Can be 'speed', 'jump', 'shield', etc.

# Duration of the powerup effect in seconds
@export var effect_duration: float = 5.0

func _ready():
	# Connect to body entered signal to detect when players touch the powerup
	body_entered.connect(_on_body_entered)

# Called when a body (player) enters the powerup's area
func _on_body_entered(body):
	# Check if the body can apply powerups (has the apply_powerup method)
	if body.has_method("apply_powerup"):
		# Apply the powerup effect to the player
		body.apply_powerup(powerup_type, effect_duration)
		queue_free()  # Remove the power-up after pickup
