# Installation & Running (Godot C# .NET Version)

## Prerequisites
- **Godot Mono version** (with C#/.NET support) – [Download here](https://godotengine.org/download/mono)
- **.NET 6 SDK or later** – [Download here](https://dotnet.microsoft.com/download)
- Port forwarding enabled for your chosen TCP port (e.g., `7777`)

---

## Steps

### 1. Clone the repository
```bash
git clone <repository_url>

2. Open the project in Godot

	Launch Godot Mono.

	Click Import → Select project.godot inside the banner-bash folder.

	Click Import & Edit.

3. Build the server

Open a terminal or command prompt and run:
cd path/to/banner-bash
build_Server.bat

cd banner-bash/servercode/GameServer
dotnet run -c Release

5. Ensure port forwarding

	Forward your chosen TCP port (e.g., 7777) in your router settings to your machine’s local IP.

	Allow the port through your firewall so clients can connect.

6. Run the client

	In Godot, click the Play ▶️ button to build and run the client.

	In the game menu:

		Enter your IP address (external for remote clients, 127.0.0.1 for local testing).

		Enter the port number you configured.

		Click JOIN.

Always start the server before running the client.

Ensure your firewall and router allow traffic on the selected port.
