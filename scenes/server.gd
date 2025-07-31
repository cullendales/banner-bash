extends Node3D

const PORT : int = 7777 #using 7777 but can be changed
var server = TCPServer.new() #should be acceptable, just lets us do sockets ourselves

var clients = []
var flag_held = false # determines if player has flag
var has_flag = null # will later hold current flag holder


func _ready():
	var server_load = server.listen(PORT)
	# 0 means it loaded OK. This checks to see if server loaded properly
	if server_load == 0:
		print("Server loaded successfully")
	else:
		print("Server failed to load")
	
func _process(delta):
	#to do
	
	
