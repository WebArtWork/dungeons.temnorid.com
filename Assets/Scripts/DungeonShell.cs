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
				// One player, straight into the lord's arena after signing in: no landing screen, no lobby, no Profile for now.
				Multiplayer = false,
				Profile = false,
				SkipLanding = true,
				GameScene = "Main",
				Accent = new Color(1f, 0.82f, 0.3f),
			};
		}
	}
}
