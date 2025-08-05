extends CanvasLayer

@onready var label_flag_holder = $LabelFlagHolder
@onready var label_p1_score = $LabelPlayer1Score
@onready var label_p2_score = $LabelPlayer2Score
@onready var stamina_bar = $StaminaBar

var local_player = null
var flag = null
var networkManager = null
var player_scores = {}  # Dictionary to store all player scores

func _ready():
	# Wait a frame to ensure the scene is fully loaded
	await get_tree().process_frame
	
	# Find NetworkManager
	networkManager = get_tree().get_root().get_node_or_null("Map/Server")
	
	# Try to find local player through NetworkManager first
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
		if flag.holder and label_flag_holder != null:
			label_flag_holder.text = "Flag Holder: " + flag.holder.name
		elif label_flag_holder != null:
			label_flag_holder.text = "Flag Holder: None"

		# Update local player score
		if label_p1_score != null:
			label_p1_score.text = "%s Score: %d" % [local_player.name, int(local_player.score)]
		if stamina_bar != null:
			stamina_bar.value = local_player.stamina_current
		
		# Update all player scores
		update_all_player_scores()
	elif local_player == null:
		if label_flag_holder != null:
			label_flag_holder.text = "Flag Holder: None"
		if label_p1_score != null:
			label_p1_score.text = "Player Score: 0"
		if label_p2_score != null:
			label_p2_score.text = "Player 2 Score: 0"
		if stamina_bar != null:
			stamina_bar.value = 0

# Function to update a specific player's score
func update_player_score(player_id: int, score: float):
	player_scores[player_id] = score
	print("HUD: Updated player %d score to %d" % [player_id, int(score)])
	print("HUD: Current player_scores: ", player_scores)

# Function to update all player scores in the UI
func update_all_player_scores():
	print("HUD: update_all_player_scores called")
	print("HUD: networkManager = ", networkManager != null)
	print("HUD: player_scores = ", player_scores)
	
	if networkManager == null:
		print("HUD: networkManager is null, returning")
		return
		
	# Get local player ID from NetworkManager
	var local_player_id = -1
	if networkManager.has_method("GetMyClientId"):
		local_player_id = networkManager.GetMyClientId()
		print("HUD: local_player_id = ", local_player_id)
	
	# Update local player score
	if local_player != null:
		player_scores[local_player_id] = local_player.score
		if label_p1_score != null:
			label_p1_score.text = "Player %d Score: %d" % [local_player_id, int(local_player.score)]
		print("HUD: Updated local player %d score to %d" % [local_player_id, int(local_player.score)])
	
	# Update other players' scores from our stored data
	for player_id in player_scores:
		if player_id != local_player_id:  # Don't update local player here
			var player_score = player_scores[player_id]
			print("HUD: Updating other player %d score to %d" % [player_id, int(player_score)])
			if player_id == 2 and label_p2_score != null:
				label_p2_score.text = "Player %d Score: %d" % [player_id, int(player_score)]
			# Add more labels for additional players as needed
