using System.Collections;
using System.IO;
using ITKamianets.Engine.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Dungeons.Tests
{
	public class DungeonRunTests
	{
		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "dungeons-playmode-" + System.Guid.NewGuid());
			DungeonGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			DungeonGame.SaveRootOverride = null;
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		static IEnumerator Load()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
		}

		static void Teleport(Transform who, Vector3 position)
		{
			var controller = who.GetComponent<CharacterController>();
			controller.enabled = false;
			who.position = position;
			controller.enabled = true;
		}

		[UnityTest]
		public IEnumerator Every_monster_stands_on_the_baked_floor_and_the_run_starts_clean()
		{
			yield return Load();
			var game = DungeonGame.Instance;
			game.SaveEnabled = false;

			Assert.AreEqual(6, game.StartingMonsters.Length, "five monsters and the boss");
			foreach (var monster in game.StartingMonsters)
			{
				Assert.IsTrue(monster.gameObject.activeSelf, monster.name + " is awake");
				Assert.IsTrue(monster.Agent.Nav.isOnNavMesh, monster.name + " stands on the NavMesh");
			}
			Assert.AreEqual(DungeonPhase.Playing, game.Phase);
			Assert.AreEqual(6, game.MonstersLeft);
			Assert.IsFalse(game.Gates[0].IsOpen);
			Assert.IsFalse(game.Gates[1].IsOpen);
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator Monsters_notice_the_player_chase_and_hurt_them()
		{
			yield return Load();
			var game = DungeonGame.Instance;
			game.SaveEnabled = false;

			// Stand at the doorway of the first room, in sight of its monsters.
			Teleport(game.PlayerHealth.transform, new Vector3(0f, 0.1f, 16.5f));
			Time.timeScale = 4f;

			var deadline = Time.realtimeSinceStartup + 40f;
			while (game.PlayerHealth.Current >= game.PlayerHealth.MaxHealth && Time.realtimeSinceStartup < deadline) yield return null;
			Time.timeScale = 1f;

			Assert.Less(game.PlayerHealth.Current, game.PlayerHealth.MaxHealth, "a monster reached the player and hit them");
		}

		[UnityTest]
		public IEnumerator Clearing_rooms_opens_the_gates_and_beating_the_boss_finishes_the_run()
		{
			yield return Load();
			var game = DungeonGame.Instance;
			game.SaveEnabled = false;

			void Kill(int room, bool boss)
			{
				foreach (var monster in game.StartingMonsters)
				{
					if (monster.Room == room && monster.IsBoss == boss && monster.Health.IsAlive)
						monster.Health.ApplyDamage(new DamageInfo(9999f));
				}
			}

			Kill(0, false);
			Assert.IsTrue(game.Gates[0].IsOpen, "room A is cleared, its gate opens");
			Assert.IsFalse(game.Gates[1].IsOpen);
			Kill(1, false);
			Assert.IsTrue(game.Gates[1].IsOpen, "room B is cleared, the boss gate opens");
			Assert.IsFalse(game.BossDead);

			game.TryFinish();
			Assert.AreEqual(DungeonPhase.Playing, game.Phase, "the exit is not open yet");

			Kill(2, true);
			Assert.IsTrue(game.BossDead);
			Assert.IsTrue(game.Chest.CanOpen, "the chest appears");
			Assert.IsTrue(game.Exit.IsOpen, "the exit opens");

			game.TryFinish();
			Assert.AreEqual(DungeonPhase.Playing, game.Phase, "the chest must be opened first");

			game.OpenChest();
			Assert.AreEqual(1, game.Session.Stash.Count(Loot.EmberBlade), "the artefact is in the stash");
			Assert.Greater(game.Session.Stash.Count(Loot.Gold), 0);

			game.TryFinish();
			Assert.AreEqual(DungeonPhase.Victory, game.Phase);
			Assert.AreEqual(1, game.Session.Completions);
			Assert.Greater(game.Session.BestSeconds, 0f);
		}

		[UnityTest]
		public IEnumerator Dying_ends_the_run_in_defeat()
		{
			yield return Load();
			var game = DungeonGame.Instance;
			game.SaveEnabled = false;

			game.PlayerHealth.ApplyDamage(new DamageInfo(9999f));
			Assert.AreEqual(DungeonPhase.Defeat, game.Phase);
		}
	}
}
