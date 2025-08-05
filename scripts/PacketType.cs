// Packet types for network communication
public enum PacketType : byte
{
	Welcome = 1,
	PlayerPosition = 2,
	PlayerState = 3,
	FlagPickupRequest = 4,
	FlagDropRequest = 5,
	FlagState = 6,
	PlayerJoined = 7,
	PlayerLeft = 8,
	Attack = 9,
	TakeHit = 10,
	SlotRequest = 11,
	PowerupSpawn = 12,
	PowerupPickup = 13,
	GameState = 14
} 
