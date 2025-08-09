using System;
using GameServer;

namespace GameServer
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.Title = "Game Server"; // the console window title

			Server.Start(8, 7777); // uses 7777 as the port number and allows 8 max players to join the game

			Console.ReadKey(); // keeps server running and terminates upon keystroke
		}
	}
}
