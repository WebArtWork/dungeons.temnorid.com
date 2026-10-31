using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>What the shared shell (splash, landing, lobby, top and bottom bars) needs to know about this game.</summary>
	public static class DungeonShell
	{
		public const string Repo = "WebArtWork/dungeons.temnorid.com";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void Configure()
		{
			ShellConfig.Current = new ShellConfig
			{
				Title = "Temnorid Dungeons",
				Tagline = "Clear the ruins. Beat the boss. Keep the loot.",
				GameId = "dungeons",
				Repo = Repo,
				InstallerSuffix = "-Setup.exe",
				MaxPlayers = 4,
				Multiplayer = true,
				GameScene = "Main",
				Accent = new Color(1f, 0.82f, 0.3f),
				Places =
				{
					new ShellPlace("dungeon", "Dungeon", "D"),
					new ShellPlace("hero", "Hero", "H"),
				},
			};
		}
	}
}
