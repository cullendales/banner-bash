extends CanvasLayer

@onready var label_flag_holder = $LabelFlagHolder
@onready var label_p1_score = $LabelPlayer1Score
@onready var stamina_bar = $StaminaBar

var local_player = null
var flag = null

func _ready():
	# Wait a frame to ensure the scene is fully loaded
	await get_tree().process_frame
	
	# Try to find local player through NetworkManager first
	var networkManager = get_tree().get_root().get_node_or_null("Map/Server")
	if networkManager != null and networkManager.has_method("GetLocalPlayer"):
		local_player = networkManager.GetLocalPlayer()
	
	# Fallback: Find the local player (Character node) directly
	if local_player == null:
		local_player = get_tree().get_root().get_node_or_null("Map/Game/Character")
		if local_player == null:
			# Try alternative paths
			local_player = get_tree().get_root().get_node_or_null("Map/Character")
	
	# Find the flag
	flag = get_tree().get_root().get_node_or_null("Map/Game/Flag")
	if flag == null:
		flag = get_tree().get_root().get_node_or_null("Map/Flag")
	
	# Initialize stamina bar if player is found
	if local_player != null:
		stamina_bar.min_value = 0
		stamina_bar.max_value = local_player.stamina_max
		stamina_bar.value = local_player.stamina_current
		print("HUD: Found local player and initialized stamina bar")
	else:
		print("HUD: Local player not found, will retry in _process")

func _process(delta):
	# Try to find local player if not found yet
	if local_player == null:
		# Try NetworkManager first
		var networkManager = get_tree().get_root().get_node_or_null("Map/Server")
		if networkManager != null and networkManager.has_method("GetLocalPlayer"):
			local_player = networkManager.GetLocalPlayer()
		
		# Fallback: direct path
		if local_player == null:
			local_player = get_tree().get_root().get_node_or_null("Map/Game/Character")
			if local_player == null:
				local_player = get_tree().get_root().get_node_or_null("Map/Character")
		
		if local_player != null:
			stamina_bar.min_value = 0
			stamina_bar.max_value = local_player.stamina_max
			stamina_bar.value = local_player.stamina_current
			print("HUD: Found local player in _process")
	
	# Try to find flag if not found yet
	if flag == null:
		flag = get_tree().get_root().get_node_or_null("Map/Game/Flag")
		if flag == null:
			flag = get_tree().get_root().get_node_or_null("Map/Flag")
	
	# Update UI only if we have the required nodes
	if local_player != null and flag != null:
		if flag.holder:
			label_flag_holder.text = "Flag Holder: " + flag.holder.name
		else:
			label_flag_holder.text = "Flag Holder: None"

		label_p1_score.text = "%s Score: %d" % [local_player.name, int(local_player.score)]
		stamina_bar.value = local_player.stamina_current
	elif local_player == null:
		label_flag_holder.text = "Flag Holder: None"
		label_p1_score.text = "Player Score: 0"
		stamina_bar.value = 0
