extends CanvasLayer

@onready var label_flag_holder: Label = $LabelFlagHolder
@onready var label_p1_score: Label = $LabelPlayer1Score
@onready var label_p2_score: Label = $LabelPlayer2Score
@onready var stamina_bar: ProgressBar = $StaminaBar
@onready var scores_container: Control = get_node_or_null("ScoresVBox")

var local_player: Node = null
var flag: Node = null
var networkManager: Node = null

var player_scores := {}    # pid -> float
var score_labels := {}     # pid -> Label

func _ready():
	await get_tree().process_frame

	networkManager = get_tree().get_root().get_node_or_null("Map/Server")

	# Find local player
	if networkManager != null and networkManager.has_method("GetLocalPlayer"):
		local_player = networkManager.GetLocalPlayer()
	if local_player == null:
		local_player = get_tree().get_root().get_node_or_null("Map/Game/Character")
		if local_player == null:
			local_player = get_tree().get_root().get_node_or_null("Map/Character")

	# Find flag
	flag = get_tree().get_root().get_node_or_null("Map/Game/Flag")
	if flag == null:
		flag = get_tree().get_root().get_node_or_null("Map/Flag")

	# Init stamina
	if local_player != null and stamina_bar != null:
		stamina_bar.min_value = 0
		stamina_bar.max_value = local_player.stamina_max
		stamina_bar.value = local_player.stamina_current

func _process(_delta):
	# Lazy find local player if needed
	if local_player == null:
		if networkManager == null:
			networkManager = get_tree().get_root().get_node_or_null("Map/Server")
		if networkManager != null and networkManager.has_method("GetLocalPlayer"):
			local_player = networkManager.GetLocalPlayer()
		if local_player == null:
			local_player = get_tree().get_root().get_node_or_null("Map/Game/Character")
			if local_player == null:
				local_player = get_tree().get_root().get_node_or_null("Map/Character")
		if local_player != null and stamina_bar != null:
			stamina_bar.min_value = 0
			stamina_bar.max_value = local_player.stamina_max
			stamina_bar.value = local_player.stamina_current

	# Lazy find flag if needed
	if flag == null:
		flag = get_tree().get_root().get_node_or_null("Map/Game/Flag")
		if flag == null:
			flag = get_tree().get_root().get_node_or_null("Map/Flag")

	# Flag holder text 
	if flag != null and label_flag_holder != null:
		var holder_name = (flag.holder.name if flag.holder != null else "None")
		label_flag_holder.text = "Flag Holder: %s" % holder_name

	# Stamina
	if local_player != null and stamina_bar != null:
		stamina_bar.value = local_player.stamina_current

	

func update_player_score(player_id: int, score: float):
	player_scores[player_id] = score

	# Ensure one label per player
	if not score_labels.has(player_id):
		var lbl := Label.new()
		lbl.name = "Score_%d" % player_id
		lbl.text = "Player %d: 0" % player_id

		if scores_container:
			scores_container.add_child(lbl)
		else:
			add_child(lbl)
			# Manual stacking if you don't have a container
			var idx := score_labels.size()
			lbl.position = Vector2(20, 20 + idx * 24)

		score_labels[player_id] = lbl

	score_labels[player_id].text = "Player %d: %d" % [player_id, int(score)]

func remove_player_row(player_id:int) -> void:
	if score_labels.has(player_id):
		score_labels[player_id].queue_free()
		score_labels.erase(player_id)
		if not scores_container:
			var i := 0
			for lbl in score_labels.values():
				lbl.position = Vector2(20, 20 + i * 24)
				i += 1
