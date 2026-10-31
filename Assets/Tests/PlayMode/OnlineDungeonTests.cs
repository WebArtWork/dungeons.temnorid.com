using System;
using System.Collections;
using System.IO;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Dungeons.Tests
{
	/// <summary>
	/// The real dungeon with this machine as the host and a second player (a detached session in the same process) as the guest:
	/// the guest's hero is shown on the host from its pose, the host's monsters reach the guest as entities, and a hit by the
	/// guest goes to the host, which applies it and tells everybody the new health. Also the touch controls.
	/// </summary>
	public class OnlineDungeonTests
	{
		NetSession _guest;
		string _saveFolder;

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 10f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "dungeons-online-" + Guid.NewGuid());
			DungeonGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			Time.timeScale = 1f;
			DungeonHud.ForceTouch = false;
			DungeonGame.SaveRootOverride = null;
			if (_guest != null) { _guest.Leave(); UnityEngine.Object.Destroy(_guest.gameObject); }
			NetSession.Instance.Leave();
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator A_guest_fights_in_the_hosts_dungeon_and_the_host_stays_the_authority()
		{
			var host = NetSession.Instance;
			host.Leave();
			host.GameId = "dungeons";
			host.GameVersion = Application.version;
			host.LocalName = "Hosty";
			host.MaxPlayers = 4;
			Assert.IsTrue(host.HostDirect(7801), host.LastError);

			_guest = NetSession.CreateDetached();
			_guest.GameId = "dungeons";
			_guest.GameVersion = Application.version;
			_guest.LocalName = "Guesty";
			_guest.LocalColour = 3;
			Assert.IsTrue(_guest.JoinDirect("127.0.0.1:7801"), _guest.LastError);
			yield return Until(() => host.Players.Count == 2 && _guest.Players.Count == 2 && !_guest.Connecting, "the guest to join");

			string damage = null;
			string killed = null;
			string loot = null;
			_guest.On(DungeonNet.LootEvent, (who, text) => loot = text);
			_guest.On(DungeonNet.DamageEvent, (who, text) => damage = text);
			_guest.On(DungeonNet.KillEvent, (who, text) => killed = text);

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;

			var game = DungeonGame.Instance;
			game.SaveEnabled = false;
			Assert.IsNotNull(game.Net, "an online dungeon has the link to other players");
			Assert.IsTrue(game.Net.IsHost);
			foreach (var monster in game.StartingMonsters)
			{
				Assert.IsFalse(monster.Proxy, "the host simulates the monsters");
				Assert.IsTrue(monster.Agent.Nav.isOnNavMesh, monster.name + " stands on the NavMesh");
			}
			Assert.AreEqual(40f * 1.5f, game.StartingMonsters[0].Health.MaxHealth, 0.01f, "two heroes, tougher monsters");
			Assert.AreEqual(180f, game.PlayerHealth.MaxHealth, 0.01f, "the hero is as before");

			// The guest says where its hero is: the host shows it, next to the first goblin.
			var target = new Vector3(-4f, 0.1f, 19.5f);
			for (var i = 0; i < 6; i++)
			{
				_guest.PublishPose(new NetPose { Position = target, Yaw = 30f, Speed = 3f, State = 0, A = 150f, B = 180f });
				yield return new WaitForSeconds(0.1f);
			}
			yield return Until(() => game.Net.Heroes.Count == 1, "the guest's hero to appear");
			RemoteHero shown = null;
			foreach (var hero in game.Net.Heroes.Values) shown = hero;
			Assert.AreEqual("Guesty", shown.DisplayName);
			yield return Until(() => (shown.transform.position - target).magnitude < 0.5f, "the guest's hero to be shown where it is");
			Assert.AreEqual(150f, shown.Health, 0.01f);
			Assert.IsFalse(shown.Dead);

			// The host's monsters reach the guest as entities (a goblin, a skeleton and the boss among them).
			yield return Until(() => _guest.Entities.Count >= 6, "the monsters' states");
			Assert.AreEqual(1, _guest.Entities[1].Kind);
			Assert.AreEqual(2, _guest.Entities[3].Kind);
			Assert.AreEqual(3, _guest.Entities[6].Kind);
			Assert.AreEqual(60f, _guest.Entities[1].Value, 0.01f);

			// A hit from the guest is a request: the host checks it, applies it, and the new health reaches the guest.
			_guest.Send(DungeonNet.HitEvent, "1|melee", true);
			var goblin = game.StartingMonsters[0];
			yield return Until(() => goblin.Health.Current < 60f, "the host to apply the guest's hit");
			Assert.AreEqual(60f - 18f, goblin.Health.Current, 0.01f, "the damage is the host's own number for a sword hit");
			yield return Until(() => _guest.Entities.TryGetValue(1, out var e) && e.Value < 60f, "the guest to see the goblin's new health");
			Assert.AreEqual(42f, _guest.Entities[1].Value, 0.5f);

			// A hit the guest cannot reach is refused (the boss is far away).
			_guest.Send(DungeonNet.HitEvent, "6|melee", true);
			yield return new WaitForSeconds(0.5f);
			Assert.AreEqual(game.StartingMonsters[5].Health.MaxHealth, game.StartingMonsters[5].Health.Current, 0.01f);

			// A monster's hit on the guest's hero goes to its owner.
			game.Net.DamageHero(shown, 7f, "melee");
			yield return Until(() => damage != null, "the guest to be told it was hit");
			StringAssert.StartsWith("7", damage);

			// Killing a monster tells the guest. The drop falls beside the guest's hero, so the host hands it to the guest's owner.
			goblin.Health.ApplyDamage(new ITKamianets.Engine.Combat.DamageInfo(9999f));
			yield return Until(() => killed == "1", "the guest to be told the goblin died");
			yield return Until(() => loot != null, "the guest to be given the drop it stands on");
			Assert.AreEqual("gold|5", loot);
			Assert.AreEqual(0, game.Session.Stash.Count(Loot.Gold), "the host's own stash is untouched");
			Assert.AreEqual(1, game.Kills);
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator A_phone_gets_a_stick_a_look_area_and_buttons()
		{
			DungeonHud.ForceTouch = true;
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
			DungeonGame.Instance.SaveEnabled = false;

			var hud = UnityEngine.Object.FindAnyObjectByType<DungeonHud>();
			Assert.IsTrue(hud.HasTouchControls);
			Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<OnScreenStick>().Length, "one move stick");
			Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TouchLookArea>().Length, "one look area");
			Assert.AreEqual(5, UnityEngine.Object.FindObjectsByType<OnScreenButton>().Length, "attack, wave, jump, use, run");

			var found = false;
			foreach (var device in InputSystem.devices) if (device is EngineTouchControls) found = true;
			Assert.IsTrue(found, "the touch controls feed the engine's virtual touch device");
		}
	}
}
