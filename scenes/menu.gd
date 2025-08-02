# res://scripts/menu.gd
extends Control

@onready var ip_line : LineEdit = $IP      

func _on_HostButton_pressed() -> void:
	Networking.host()
	_start_game()

func _on_JoinButton_pressed() -> void:
	Networking.join(ip_line.text.strip_edges())
	_start_game()

func _start_game() -> void:
	get_tree().change_scene_to_file("res://scenes/map.tscn")
	# GameManager.gd (inside map.tscn) will call Network.send_hello() on _ready().
