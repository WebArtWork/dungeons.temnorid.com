using ITKamianets.Engine.AI;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.Input;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Temnorid.Dungeons
{
	public enum DungeonPhase
	{
		Playing,
		Victory,
		Defeat,
	}

	/// <summary>
	/// Runs one trip through the dungeon: bakes the NavMesh, wakes the monsters, opens each gate when its room
	/// is cleared, drops loot, reveals the chest when the boss dies, and ends the run in victory or defeat.
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class DungeonGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

		public static DungeonGame Instance { get; private set; }

		[Header("Scene")]
		public Health PlayerHealth;
		public NavMeshSurface Surface;
		public Monster[] StartingMonsters;
		public Monster Boss;
		public RoomGate[] Gates;
		public Chest Chest;
		public ExitPortal Exit;

		[Header("Rewards")]
		public int ChestGold = 50;

		public bool SaveEnabled = true;

		const string Log = "Dungeons";

		DungeonSaveStore _store;

		public DungeonSession Session { get; private set; }
		public DungeonPhase Phase { get; private set; } = DungeonPhase.Playing;
		public bool Paused { get; private set; }

		/// <summary>The player opened another place (hero, profile): the game waits, offline, without showing the pause menu.</summary>
		public bool UiHold { get; private set; }

		/// <summary>The link to the other players when this is an online game, else null.</summary>
		public DungeonNet Net { get; private set; }

		/// <summary>Online: this hero has fallen and watches the others; the run goes on until everybody has fallen or the boss is beaten.</summary>
		public bool LocalDown { get; private set; }

		/// <summary>Monsters slain this run, by the whole party.</summary>
		public int Kills { get; private set; }
		public float RunTime { get; private set; }
		public bool BossDead { get; private set; }
		public bool NewBestTime { get; private set; }

		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }

		void Awake()
		{
			Instance = this;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;

			Session = new DungeonSession();
			_store = new DungeonSaveStore(SaveRootOverride);
			Session.Load(_store.Load());
			Session.StartRun();

			// The monsters wait, switched off, until the floor exists to walk on.
			Surface.BuildNavMesh();
			foreach (var monster in StartingMonsters) monster.gameObject.SetActive(true);

			if (NetSession.Instance.IsOnline) Net = gameObject.AddComponent<DungeonNet>().Init(this);

			PlayerHealth.Died += OnPlayerDied;
		}

		void OnEnable() => Monster.Killed += OnMonsterKilled;

		void OnDisable() => Monster.Killed -= OnMonsterKilled;

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		void Update()
		{
			if (Phase != DungeonPhase.Playing) return;
			if ((!Paused && !UiHold) || Net != null) RunTime += Time.deltaTime;
			if (!UiHold && EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!Paused);
		}

		void OnApplicationQuit() => Save();

		public int MonstersLeft => Monster.All.Count;

		public void SetPaused(bool paused)
		{
			if (Phase != DungeonPhase.Playing) return;
			Paused = paused;
			ApplyHold();
			if (paused) Save();
		}

		/// <summary>Another place of the shell is open over the dungeon.</summary>
		public void SetUiHold(bool hold)
		{
			if (Phase != DungeonPhase.Playing || UiHold == hold) return;
			UiHold = hold;
			ApplyHold();
		}

		// Online the dungeon never stops for one person: the menus open over it.
		void ApplyHold()
		{
			var hold = Paused || UiHold;
			Time.timeScale = hold && Net == null ? 0f : 1f;
			EngineInput.Default.PlayerEnabled = !hold;
		}

		/// <summary>Saves, then leaves for the landing screen (closing any online game).</summary>
		public void QuitToMenu()
		{
			Save();
			ShellFlow.QuitToMenu();
		}

		// ---- what happens in the dungeon ----

		void OnMonsterKilled(Monster monster)
		{
			if (monster.LootQuantity > 0)
			{
				var drop = LootPickup.Spawn(monster.LootId, monster.LootQuantity, monster.transform.position);
				Net?.Register(drop);
			}
			if (monster.HealDrop > 0f)
			{
				var orb = LootPickup.Spawn(Loot.HealthOrb, Mathf.RoundToInt(monster.HealDrop), monster.transform.position + new Vector3(1f, 0f, 0.6f));
				Net?.Register(orb);
			}

			Kills++;
			Net?.MonsterKilled(monster);
			if (monster.IsBoss) BossFell(monster.transform.position);

			for (var i = 0; i < Gates.Length; i++)
			{
				var gate = Gates[i];
				if (gate.IsOpen || AnyAliveIn(gate.Room)) continue;
				gate.Open();
				Net?.GateOpened(i);
				ShowToast("A door opens.");
			}
		}

		/// <summary>The boss is dead, here or (on a client) on the host: the chest appears and the exit opens.</summary>
		public void BossFell(Vector3 position)
		{
			if (BossDead) return;
			BossDead = true;
			Chest.Reveal(position);
			Exit.Open();
			ShowToast("The boss has fallen! Open the chest.");
			EngineLog.Info(Log, "The boss is dead.");
		}

		/// <summary>Client: the host says a monster died.</summary>
		public void CountKill() => Kills++;

		static bool AnyAliveIn(int room)
		{
			foreach (var monster in Monster.All) if (monster.Room == room) return true;
			return false;
		}

		/// <summary>The player picked loot up off the floor.</summary>
		public void Collect(string itemId, int quantity)
		{
			if (itemId == Loot.HealthOrb)
			{
				PlayerHealth.Heal(quantity);
				ShowToast($"+{quantity} health");
				return;
			}
			var added = Session.AddLoot(itemId, quantity);
			if (added > 0) ShowToast($"+{added} {Loot.Name(itemId)}");
			Save();
		}

		/// <summary>The player opens the chest the boss guarded.</summary>
		public void OpenChest()
		{
			if (!Chest.CanOpen) return;
			Chest.Open();
			Session.AddLoot(Loot.Gold, ChestGold);
			Session.AddLoot(Loot.EmberBlade, 1);
			Session.AddLoot(Loot.BossSigil, 1);
			ShowToast("Chest opened: gold, the Ember Blade and a Boss Sigil!");
			Save();
		}

		/// <summary>The player stepped into the exit.</summary>
		public void TryFinish()
		{
			if (Phase != DungeonPhase.Playing) return;
			if (!Chest.Opened)
			{
				ShowToast("Take the treasure from the chest first.");
				return;
			}
			if (Net != null)
			{
				// Online the host ends the run for everybody once the living heroes are out (or after a short wait).
				ShowToast("Waiting for the others to leave...");
				Net.Finish();
				return;
			}
			EndRun(true);
		}

		/// <summary>Ends the run: victory or defeat. Online it is the host that decides and tells everybody.</summary>
		public void EndRun(bool victory)
		{
			if (Phase != DungeonPhase.Playing) return;
			Time.timeScale = 1f;
			Phase = victory ? DungeonPhase.Victory : DungeonPhase.Defeat;
			if (victory)
			{
				NewBestTime = Session.CompleteRun(RunTime);
				EngineLog.Info(Log, $"Dungeon cleared in {RunTime:0.0}s.");
			}
			else
			{
				var marker = PlayerHealth.GetComponent<AiTarget>();
				if (marker != null) marker.Alive = false;
			}
			Save();
			StopPlay();
		}

		void OnPlayerDied(Health health, DamageInfo info)
		{
			if (Phase != DungeonPhase.Playing) return;
			var marker = health.GetComponent<AiTarget>();
			if (marker != null) marker.Alive = false;
			if (Net != null)
			{
				GoDown(health);
				return;
			}
			EndRun(false);
		}

		// Online: a fallen hero stays where it fell and looks around; the others fight on.
		void GoDown(Health health)
		{
			LocalDown = true;
			var controls = health.GetComponent<CharacterViewControls>();
			var motor = health.GetComponent<CharacterMotor>();
			if (controls != null) controls.enabled = false;
			if (motor != null) motor.SetCommand(new CharacterCommand { Yaw = motor.Yaw });
			var body = health.transform.Find("Body");
			if (body != null) body.localRotation = Quaternion.Euler(-80f, 0f, 0f);
			ShowToast("You have fallen. Your allies fight on.");
		}

		void StopPlay()
		{
			EngineInput.Default.PlayerEnabled = false; // unlocks the cursor for the buttons
		}

		/// <summary>Starts the dungeon again from the entrance.</summary>
		public void Retry()
		{
			if (Net != null) return; // the others cannot follow a reload: leave and host again
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		public void ShowToast(string text)
		{
			ToastText = text;
			ToastUntil = Time.unscaledTime + 3f;
		}

		public void Save()
		{
			if (SaveEnabled && Session != null) _store.Save(Session.ToSave());
		}

		public void SaveAndQuit()
		{
			Save();
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}
	}
}
