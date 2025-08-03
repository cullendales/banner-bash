extends CanvasLayer

@onready var label_flag_holder = $LabelFlagHolder
@onready var label_p1_score = $LabelPlayer1Score
@onready var stamina_bar = $StaminaBar


var player1 = null
var player2 = null
var flag = null

func _ready():
	var game = get_tree().get_root().get_node("Map/Game")
	player1 = game.get_node("Player 1")
	flag = game.get_node("Flag")
	stamina_bar.min_value = 0
	stamina_bar.max_value = player1.stamina_max
	stamina_bar.value = player1.stamina_current

func _process(delta):
	if flag.holder:
		label_flag_holder.text = "Flag Holder: " + flag.holder.name
	else:
		label_flag_holder.text = "Flag Holder: None"

	label_p1_score.text = "%s Score: %d" % [player1.name, int(player1.score)]
	stamina_bar.value = player1.stamina_current
