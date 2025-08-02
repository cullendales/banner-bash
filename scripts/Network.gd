# res://scripts/Network.gd
extends Node

# ---------------------------------------------------------------------
#  CONFIG
# ---------------------------------------------------------------------
const PORT       : int   = 7777
const MAX_PLAYERS: int   = 4
const TICK_RATE  : float = 15.0         # server → client snapshots / sec
const STATE_RATE : float = 0.05         # client → server (seconds)

# ---------------------------------------------------------------------
#  PUBLIC STATE  (other scripts read / write these)
# ---------------------------------------------------------------------
var is_server : bool   = false          # true ↔︎ host, false ↔︎ client
var my_id     : int    = -1             # set after «welcome»
var local_state : Dictionary            # Character.gd fills this each frame

var players : Dictionary = {}           # id → {pos,rot,is_crouch,is_sprint}
var flag    : Dictionary = {            # authoritative flag data
	"holder": -1,
	"pos"   : Vector3.ZERO,
}
var scores  : Dictionary = {}           # id → float

# ---------------------------------------------------------------------
#  PRIVATE
# ---------------------------------------------------------------------
var _server        : TCPServer          = TCPServer.new()
var _clients       : Array[StreamPeerTCP] = []
var _tcp           : StreamPeerTCP      # client side socket
var _tick_accum    : float = 0.0
var _state_accum   : float = 0.0

# ---------------------------------------------------------------------
#  API ----------------------------------------------------------------
# ---------------------------------------------------------------------
func host() -> void:
	randomize()
	is_server = true
	var err := _server.listen(PORT)
	if err != OK:
		push_error("Server failed to listen on port %d (err %d)" % [PORT, err])
		return
	# give the host itself an id so its Character scene can publish state
	my_id = _gen_id()
	players[my_id] = _empty_player_state()
	scores[my_id]  = 0
	print("✔ Server listening on %d" % PORT)

func join(ip: String) -> void:
	is_server = false
	_tcp = StreamPeerTCP.new()
	var err := _tcp.connect_to_host(ip, PORT)
	if err != OK:
		push_error("Connect error %d" % err)
		return
	print("🔌 Connecting to %s:%d…" % [ip, PORT])

func _user_display_name() -> String:
	if OS.has_environment("USERNAME"):       # Windows, WSL
		return OS.get_environment("USERNAME")
	if OS.has_environment("USER"):           # Linux, macOS
		return OS.get_environment("USER")
	return "Player"                          # fallback


func send_hello() -> void:
	# called once from GameManager after spawning the local player
	if !is_server and _tcp and _tcp.get_status() == StreamPeerTCP.STATUS_CONNECTED:
		var pkt := {"op": "hello", "name": _user_display_name()}
		_send(_tcp, pkt)          

# ---------------------------------------------------------------------
#  MAIN LOOP
# ---------------------------------------------------------------------
func _process(delta: float) -> void:
	if is_server:
		_server_process(delta)
	else:
		_client_process(delta)

# ---------------------------------------------------------------------
#  SERVER PART
# ---------------------------------------------------------------------
func _server_process(delta: float) -> void:
	# accept new sockets ------------------------------------------------
	while _server.is_connection_available():
		var p := _server.take_connection()
		if _clients.size() >= MAX_PLAYERS:
			p.disconnect_from_host(); continue
		_clients.append(p)
		print("➕ client connected")

	# read messages -----------------------------------------------------
	for p in _clients.duplicate():
		if p.get_status() != StreamPeerTCP.STATUS_CONNECTED:
			_drop_client(p); continue
		_read_lines(p, func(line:String):
			var msg : Variant = JSON.parse_string(line)    #   or  :Dictionary  if you only ever send dicts
			if msg is Dictionary: _handle_client_msg(p, msg))

	# host’s own state (listen-server) ----------------------------------
	if my_id != -1 and local_state:
		players[my_id] = local_state

	# broadcast world state --------------------------------------------
	_tick_accum += delta
	if _tick_accum >= 1.0 / TICK_RATE:
		var frame := {"op":"world","players":players,"flag":flag,"scores":scores}
		for p in _clients:
			_send(p, frame)
		_tick_accum = 0.0

func _handle_client_msg(peer: StreamPeerTCP, msg: Dictionary) -> void:
	var pid = peer.get_meta("id")      # may be null until 'hello'
	match msg.get("op",""):
		"hello":
			pid = _gen_id()
			peer.set_meta("id", pid)
			players[pid] = _empty_player_state()
			scores[pid]  = 0
			_send(peer, {"op":"welcome","id":pid,
						 "players":players,"flag":flag})
		"state":
			if pid != null: players[pid] = msg
		"flag_pickup":
			if pid != null: flag.holder = pid
		"flag_drop":
			if pid != null and flag.holder == pid:
				flag.holder = -1
				flag.pos    = msg.get("pos", flag.pos)
		"attack":
			pass # you can add hit-validation here later

func _drop_client(peer: StreamPeerTCP) -> void:
	var pid = peer.get_meta("id")
	if pid != null:
		players.erase(pid)
		scores.erase(pid)
		if flag.holder == pid:
			flag.holder = -1
	_clients.erase(peer)
	print("➖ client disconnected")

# ---------------------------------------------------------------------
#  CLIENT PART
# ---------------------------------------------------------------------
func _client_process(delta: float) -> void:
	if _tcp == null or _tcp.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		return

	# send our snapshot at STATE_RATE -----------------------------------
	_state_accum += delta
	if _state_accum >= STATE_RATE and local_state:
		_state_accum = 0.0
		var pkt : Dictionary = local_state.duplicate()  # copy the dict
		pkt["op"] = "state"                             # add the opcode
		_send(_tcp, pkt)

	# read server packets ----------------------------------------------
	_read_lines(_tcp, func(line:String):
		var msg : Variant = JSON.parse_string(line)    #   or  :Dictionary  if you only ever send dicts
		if msg is Dictionary: _handle_server_msg(msg))

func _handle_server_msg(msg: Dictionary) -> void:
	match msg.get("op",""):
		"welcome":
			my_id  = int(msg.id)
			players = msg.players
			flag    = msg.flag
			print("✔ joined; my id =", my_id)
		"world":
			players = msg.players
			flag    = msg.flag
			scores  = msg.scores
		"hit":
			# optional: notify Character.gd
			pass
		"game_over":
			get_tree().paused = true
			print("🏆 game over! winner =", msg.winner)

# ---------------------------------------------------------------------
#  LOW-LEVEL IO
# ---------------------------------------------------------------------
func _send(peer: StreamPeerTCP, data: Dictionary) -> void:
	if peer == null: return
	if peer.get_status() != StreamPeerTCP.STATUS_CONNECTED: return
	var json := JSON.stringify(data)
	peer.put_data((json + "\n").to_utf8_buffer())

# keep a per-socket buffer in meta so we can parse by newline
func _read_lines(peer: StreamPeerTCP, cb: Callable) -> void:
	var buf : String = peer.get_meta("buf") if peer.has_meta("buf") else ""
	while peer.get_available_bytes() > 0:
		var b := peer.get_u8()
		if b == 10:         # ‘\n’
			cb.call(buf)
			buf = ""
		else:
			buf += String.chr(b)
	peer.set_meta("buf", buf)

# ---------------------------------------------------------------------
#  HELPERS
# ---------------------------------------------------------------------
func _gen_id() -> int:
	var id := randi()
	while players.has(id):
		id = randi()
	return id

func _empty_player_state() -> Dictionary:
	return {
		"pos": Vector3.ZERO,
		"rot": [0.0, 0.0],
		"is_crouch": false,
		"is_sprint": false
	}
