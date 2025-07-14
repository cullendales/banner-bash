extends CanvasLayer

@onready var label_flag_holder = $LabelFlagHolder
@onready var label_p1_score = $LabelPlayer1Score


var player1 = null
var player2 = null
var flag = null

func _ready():
	# Adjust node paths to your scene tree
	var game = get_tree().get_root().get_node("Map/Game")
	player1 = game.get_node("Player 1")
	flag = game.get_node("Flag")

func _process(delta):
	if flag.holder:
		label_flag_holder.text = "Flag Holder: " + flag.holder.name
	else:
		label_flag_holder.text = "Flag Holder: None"

	label_p1_score.text = "%s Score: %d" % [player1.name, int(player1.score)]
	
