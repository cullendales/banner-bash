# Banner Bash Multiplayer Network Setup

This document explains how to set up and use the multiplayer networking system for Banner Bash.

## Overview

The networking system consists of:
- **Server**: C# TCP server that handles client connections and broadcasts game events
- **Client**: Godot client that connects to the server and handles game synchronization
- **NetworkManager**: Manages other players and network events
- **PacketHandler**: Handles packet creation and sending

## Setup Instructions

### 1. Build and Run the Server

1. Open the `servercode` folder in Visual Studio or your preferred C# IDE
2. Build the `GameServer` project
3. Run the server executable
4. The server will start on port 7777 by default

### 2. Configure the Godot Client

1. Add the `NetworkManager` script to your main game scene
2. Set the `PlayerScene` property to your character scene (for spawning other players)
3. Add the `Client` script as a singleton or child node
4. Add the `ConnectionStatus` script to your UI if you want connection status display

### 3. Scene Setup

#### Main Game Scene Structure:
```
Map (or your main scene)
├── Character (your player)
├── Flag
├── NetworkManager (add this)
├── Client (add this)
└── UI
    └── ConnectionStatus (optional)
```

#### NetworkManager Configuration:
- Set `PlayerScene` to your character scene
- The NetworkManager will automatically spawn other players when they join

### 4. Testing the Network

#### Single Player Test:
1. Start the server
2. Run the Godot game
3. Connect to `127.0.0.1:7777`
4. Check the console for connection messages

#### Multiplayer Test:
1. Start the server
2. Run multiple instances of the Godot game
3. Connect each to the same server
4. Move around and test interactions

## Network Protocol

### Packet Types:
- `Welcome (1)`: Server sends client ID to new connections
- `PlayerPosition (2)`: Player position and rotation updates
- `PlayerState (3)`: Player health, flag status, score, etc.
- `FlagUpdate (4)`: Flag pickup/drop events
- `PlayerJoined (5)`: New player joined notification
- `PlayerLeft (6)`: Player disconnected notification
- `Attack (7)`: Player attack events
- `TakeHit (8)`: Player hit events

### Packet Format:
All packets start with a 1-byte packet type, followed by packet-specific data.

## Features Implemented

### ✅ Basic Networking
- TCP connection handling
- Packet serialization/deserialization
- Client-server communication

### ✅ Player Synchronization
- Position and rotation updates
- Health and state synchronization
- Animation state updates

### ✅ Game Events
- Attack synchronization
- Flag pickup/drop events
- Player join/leave notifications

### ✅ Error Handling
- Connection loss detection
- Automatic disconnection handling
- Server shutdown handling

### ✅ Multiplayer Features
- Other player spawning
- Real-time position updates
- Cross-client interactions

## Usage Examples

### Sending Player Position:
```csharp
PacketHandler.SendPlayerPosition(global_position, rotation);
```

### Sending Attack:
```csharp
PacketHandler.SendAttack(attack_position);
```

### Sending Flag Pickup:
```csharp
PacketHandler.SendFlagPickup();
```

### Sending Flag Drop:
```csharp
PacketHandler.SendFlagDrop(drop_position);
```

## Troubleshooting

### Common Issues:

1. **Connection Refused**
   - Make sure the server is running
   - Check that port 7777 is not blocked by firewall
   - Verify the IP address is correct

2. **Players Not Appearing**
   - Check that NetworkManager is added to the scene
   - Verify PlayerScene is set in NetworkManager
   - Check console for error messages

3. **Network Lag**
   - Reduce update frequency in character script
   - Consider implementing interpolation
   - Check network connection quality

4. **Server Crashes**
   - Check for null reference exceptions
   - Verify all clients are properly disconnected
   - Check server console for error messages

## Performance Optimization

### Network Traffic Reduction:
- Send position updates every 10 frames instead of every frame
- Send state updates every 30 frames
- Consider compressing position data
- Implement client-side prediction

### Memory Management:
- Properly dispose of network connections
- Clean up disconnected players
- Use object pooling for frequent operations

## Future Enhancements

### Planned Features:
- [ ] UDP for faster position updates
- [ ] Client-side prediction
- [ ] Server-side validation
- [ ] Chat system
- [ ] Game state persistence
- [ ] Spectator mode
- [ ] Replay system

### Optimization Ideas:
- [ ] Packet compression
- [ ] Delta compression
- [ ] Interest management
- [ ] Bandwidth throttling
- [ ] Connection quality monitoring

## Support

If you encounter issues:
1. Check the console output for error messages
2. Verify all scripts are properly attached
3. Test with a single client first
4. Check network connectivity
5. Review the packet format documentation

## Files Overview

### Server Files:
- `servercode/GameServer/Server.cs` - Main server logic
- `servercode/GameServer/Client.cs` - Client connection handling
- `servercode/GameServer/Program.cs` - Server entry point

### Client Files:
- `scripts/Client.cs` - Network client implementation
- `scripts/PacketHandler.cs` - Packet creation and handling
- `scripts/NetworkManager.cs` - Multiplayer game management
- `scripts/ConnectionStatus.cs` - Connection status UI
- `scripts/NetworkPlayer.gd` - Other player character script

### Game Files:
- `scenes/character.gd` - Main player character (updated for networking)
- `scripts/Flag.gd` - Flag object (updated for networking) 