# Banner Bash
<img width="867" height="515" alt="Screenshot 2025-08-09 at 8 52 24 AM" src="https://github.com/user-attachments/assets/a1d5ed16-acdb-4ee4-911f-715dd7894746" />

## About

Banner Bash is an online game developed in Godot using a combination of GDScript and C#. The primary objective of the game is to capture a flag object before your opponents and hold it for as long as possible. The player holding the flag will gain points over time and the player with the most points when a round ends wins the game. When a player has the flag they are not safe from the other players and must run, as the other players can hit them to make them drop the flag and then pick it up themselves. Multiplayer players will compete online to obtain a singular flag.

Banner Bash includes jumping, crouching, sprinting, hitting, and power-up mechanics. The power-ups give the player who obtains them a temporary speed boost or higher jump. Players must also manage their sprint meter, as players who run out of stamina must wait until it recharges to begin sprinting again.

The map in the game includes various objects and high points to help players avoid others and introduce some variety into gameplay. The flag begins at a central high point of the map.

## Requirements

The .net framework to run c# must be downloaded on your local machine to run the server for this game. The 4.4 .net version of Godot must also be installed for either MacOS or Windows to run the game itself. The regular version of Godot or an older model of Godot will not properly run the game. If you do not have fthis version, the links can be found below:

[Windows](https://godotengine.org/download/windows/)

[MacOS](https://godotengine.org/download/macos/)

Be sure to download the .net version.

## Usage

1. Clone the repository onto your local machine
2. In your terminal, navigate through the folders banner-bash/servercode/gameserver and use the command dotnet run to open the server
3. Launch Godot_mono (the .net version) and select banner-bash as your game
4. In the upper right hand corner, click the play button and wait while the game builds and launches
5. A screen will load asking for IP address and port number
   
   i. If playing on your local machine without connecting with other players, simply clicking join will allow you to enter the game
   
   ii. If connecting with other players online, every player must enter the IP address of the player who launched the server and the port number 7777 before clicking join.
7. The game will begin. Have fun!

## Controls
W / A / S / D - Move around.
Shift + Move - Sprint (hold Shift while moving).
C - Crouch.
G - Drop your flag.
Left Click - Push nearby players; 3 pushes make them drop their flag.

## Notes

This game was developed to show socket programming. No existing gaming, client-server, messaging, remote calling, or other middleware or frameworks were used. All code was written froms scratch.
