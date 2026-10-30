using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Dungeons.Tests
{
	/// <summary>
	/// Measures how hard the dungeon is: bots play whole runs at high speed and the outcome of each is written to
	/// <c>Builds/balance.txt</c>. Slow, so it only runs when asked for by name:
	/// <c>-testFilter Temnorid.Dungeons.Tests.BalanceTests.Bots_play_whole_runs</c>.
	/// </summary>
	public class BalanceTests
	{
		const int RunsPerMode = 8;

		[UnityTest]
		[Explicit("Balance measurement: run by name")]
		[Timeout(1800000)]
		public IEnumerator Bots_play_whole_runs()
		{
			DungeonGame.SaveRootOverride = Path.Combine(Path.GetTempPath(), "dungeons-balance-" + System.Guid.NewGuid());
			var report = new StringBuilder();

			foreach (var dodges in new[] { false, true })
			{
				var wins = 0;
				var totalTime = 0f;
				var totalHealth = 0f;
				for (var run = 0; run < RunsPerMode; run++)
				{
					yield return SceneManager.LoadSceneAsync("Main");
					yield return null;
					var game = DungeonGame.Instance;
					game.SaveEnabled = false;
					var bot = game.PlayerHealth.gameObject.AddComponent<AutoPlayer>();
					bot.Dodges = dodges;
					Time.timeScale = 5f;

					var deadline = Time.realtimeSinceStartup + 90f;
					while (game.Phase == DungeonPhase.Playing && game.RunTime < 300f && Time.realtimeSinceStartup < deadline) yield return null;
					Time.timeScale = 1f;

					var won = game.Phase == DungeonPhase.Victory;
					if (won)
					{
						wins++;
						totalTime += game.RunTime;
						totalHealth += game.PlayerHealth.Current;
					}
					report.AppendLine($"{(dodges ? "dodging" : "basic  ")} run {run}: {game.Phase,-8} time {game.RunTime,6:0.0}s  hp {game.PlayerHealth.Current,5:0}  monsters left {game.MonstersLeft}  boss hp {(game.Boss != null && game.Boss.Health.IsAlive ? game.Boss.Health.Current : 0):0}");
				}
				report.AppendLine($"== {(dodges ? "dodging" : "basic")}: {wins}/{RunsPerMode} won" + (wins > 0 ? $", average {totalTime / wins:0}s, {totalHealth / wins:0} hp left" : ""));
			}

			Directory.CreateDirectory("Builds");
			File.WriteAllText("Builds/balance.txt", report.ToString());
			DungeonGame.SaveRootOverride = null;
			Assert.Pass(report.ToString());
		}
	}
}
