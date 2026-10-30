using ITKamianets.Engine.AI;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Core;
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
			if (!Paused) RunTime += Time.deltaTime;
			if (EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!Paused);
		}

		void OnApplicationQuit() => Save();

		public int MonstersLeft => Monster.All.Count;

		public void SetPaused(bool paused)
		{
			if (Phase != DungeonPhase.Playing) return;
			Paused = paused;
			Time.timeScale = paused ? 0f : 1f;
			EngineInput.Default.PlayerEnabled = !paused;
			if (paused) Save();
		}

		// ---- what happens in the dungeon ----

		void OnMonsterKilled(Monster monster)
		{
			if (monster.LootQuantity > 0) LootPickup.Spawn(monster.LootId, monster.LootQuantity, monster.transform.position);
			if (monster.HealDrop > 0f) LootPickup.Spawn(Loot.HealthOrb, Mathf.RoundToInt(monster.HealDrop), monster.transform.position + new Vector3(1f, 0f, 0.6f));

			if (monster.IsBoss)
			{
				BossDead = true;
				Chest.Reveal(monster.transform.position);
				Exit.Open();
				ShowToast("The boss has fallen! Open the chest.");
				EngineLog.Info(Log, "The boss is dead.");
			}

			foreach (var gate in Gates)
			{
				if (gate.IsOpen || AnyAliveIn(gate.Room)) continue;
				gate.Open();
				ShowToast("A door opens.");
			}
		}

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
			Phase = DungeonPhase.Victory;
			NewBestTime = Session.CompleteRun(RunTime);
			Save();
			StopPlay();
			EngineLog.Info(Log, $"Dungeon cleared in {RunTime:0.0}s.");
		}

		void OnPlayerDied(Health health, DamageInfo info)
		{
			if (Phase != DungeonPhase.Playing) return;
			Phase = DungeonPhase.Defeat;
			var marker = health.GetComponent<AiTarget>();
			if (marker != null) marker.Alive = false;
			Save();
			StopPlay();
		}

		void StopPlay()
		{
			EngineInput.Default.PlayerEnabled = false; // unlocks the cursor for the buttons
		}

		/// <summary>Starts the dungeon again from the entrance.</summary>
		public void Retry()
		{
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
