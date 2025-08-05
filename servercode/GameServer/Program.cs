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

			Console.WriteLine("\nServer Commands:");
			Console.WriteLine("'scores' - Display all player scores");
			Console.WriteLine("'quit' - Exit server");
			Console.WriteLine("Press any key to continue...\n");

			// Start score update timer
			var scoreTimer = new System.Threading.Timer(UpdateScores, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

			while (true)
			{
				var input = Console.ReadLine();
				if (input != null)
				{
					switch (input.ToLower().Trim())
					{
						case "scores":
							Server.DisplayAllScores();
							break;
						case "quit":
							Console.WriteLine("Shutting down server...");
							scoreTimer.Dispose();
							return;
						default:
							Console.WriteLine("Unknown command. Use 'scores' or 'quit'");
							break;
					}
				}
			}
		}

		private static void UpdateScores(object? state)
		{
			Server.UpdateScores();
		}
	}
}
