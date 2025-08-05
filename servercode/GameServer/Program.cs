using System;
using GameServer;

namespace GameServer
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.Title = "Game Server";

			Server.Start(8, 7777);

			Console.ReadKey();
		}
	}
}
