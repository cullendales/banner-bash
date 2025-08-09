// Enumeration of all packet types used for client-server network communication.
// Each packet type corresponds to a specific game event or state update that
// needs to be synchronized between the server and all connected clients.
// The byte values are used as the first byte in each packet to identify the packet type.
public enum PacketType : byte
{
	// Server welcome message sent to new clients upon connection.
	// Contains the assigned client ID and initial game state information.
	Welcome = 1,
	
	// Player position and rotation updates sent from server to all clients.
	// Contains player ID, position (X, Y, Z), and rotation (X, Y, Z) data.
	PlayerPosition = 2,
	
	// Player state updates including health, flag status, score, stamina, and animation.
	// Sent from server to all clients when a player's state changes.
	PlayerState = 3,
	
	// Flag pickup/drop events sent from server to all clients.
	// Contains player ID, pickup/drop status, and flag position.
	FlagUpdate = 4,
	
	// Notification that a new player has joined the game.
	// Sent from server to all clients when a player connects.
	PlayerJoined = 5,
	
	// Notification that a player has left the game.
	// Sent from server to all clients when a player disconnects.
	PlayerLeft = 6,
	
	// Player attack events sent from server to all clients.
	// Contains attacker ID and attack position for damage calculation.
	Attack = 7,
	
	// Player damage events sent from server to all clients.
	// Contains player ID and damage amount when a player takes damage.
	TakeHit = 8,
	
	// Client request to join a specific player slot.
	// Sent from client to server when requesting to join the game.
	SlotRequest = 9,

	// Client → Server : request to pick up the flag (server will validate and broadcast authoritative FlagUpdate)
	RequestFlagPickup = 10,
	
	// Client → Server : request to drop the flag at a given position
	RequestFlagDrop   = 11,
	
	// Server → clients : authoritative periodic score update (playerId, score)
	PlayerScore = 12,
	
	// Server → clients : match finished, contains winner playerId
	GameWon = 13,

	PowerupSpawn = 14,
	
	// Powerup pickup events sent from server to all clients.
	PowerupPickup = 15,
	
	// General game state updates sent from server to all clients.
	GameState = 16
} 
