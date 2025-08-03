extends Node3D

var client := StreamPeerTCP.new()

func _ready():
	var err := client.connect_to_host("192.168.1.66", 7777)
	if err == OK:
		print("Connected to server!")
	else:
		print("Failed to connect: ", err)
