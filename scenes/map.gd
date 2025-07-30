# Just to give dummy the flag at start
extends Node3D

func _ready():
	
	# Wait for everything to initialize
	await get_tree().create_timer(1.0).timeout
	
	# Look for TestDummy and give it the flag once
	var dummy = $Game.get_node_or_null("TestDummy")
	if dummy:
		dummy.can_move = false  # Prevent sliding
		dummy.take_flag()
		print("Dummy has the flag!")
	else:
		print("No TestDummy found in scene")

# Debug info only
func _input(event):
	if OS.is_debug_build() and event.is_action_pressed("ui_accept"):
		var dummy = $Game.get_node_or_null("TestDummy")
		if dummy:
			print("Dummy health: %d/%d, Has flag: %s" % [dummy.current_hits, dummy.max_hits, dummy.is_flag_holder])
