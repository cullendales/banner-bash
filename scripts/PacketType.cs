// Packet types for network communication
public enum PacketType : byte
{
	Welcome = 1,
	PlayerPosition = 2,
	PlayerState = 3,
	FlagUpdate = 4,
	PlayerJoined = 5,
	PlayerLeft = 6,
	Attack = 7,
	TakeHit = 8,
	SlotRequest = 9,
	PowerupSpawn = 10,
	PowerupPickup = 11,
	GameState = 12
} 
