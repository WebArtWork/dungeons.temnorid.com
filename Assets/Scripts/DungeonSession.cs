using System;
using System.Collections.Generic;
using ITKamianets.Engine.Inventory;
using ITKamianets.Engine.Save;

namespace Temnorid.Dungeons
{
	/// <summary>The kinds of loot. Ids are what is stored in the inventory and the save.</summary>
	public static class Loot
	{
		public const string Gold = "gold";
		public const string BoneCharm = "bone-charm";
		public const string EmberBlade = "ember-blade";
		public const string BossSigil = "boss-sigil";
		/// <summary>Not an item: picking it up heals the player.</summary>
		public const string HealthOrb = "health-orb";

		public static string Name(string itemId)
		{
			switch (itemId)
			{
				case Gold: return "Gold";
				case BoneCharm: return "Bone Charm";
				case EmberBlade: return "Ember Blade";
				case BossSigil: return "Boss Sigil";
				case HealthOrb: return "Health";
				default: return itemId;
			}
		}
	}

	/// <summary>What is kept between runs: everything ever looted, how many times the dungeon was cleared, the best time.</summary>
	[Serializable]
	public sealed class DungeonSave
	{
		public InventoryData stash;
		public int completions;
		public float bestSeconds;
	}

	/// <summary>
	/// The rules of the dungeon's rewards with no Unity in them: the stash of everything looted (kept across
	/// runs), what was looted in the current run, and the record of completed runs.
	/// </summary>
	public sealed class DungeonSession
	{
		public const int StashSlots = 12;
		public const int StashStack = 999;

		readonly Dictionary<string, int> _run = new Dictionary<string, int>();

		public ItemContainer Stash { get; } = new ItemContainer(StashSlots, StashStack);
		public int Completions { get; private set; }

		/// <summary>The best time of a completed run in seconds, 0 when there is none yet.</summary>
		public float BestSeconds { get; private set; }

		/// <summary>What was looted in the run in progress.</summary>
		public IReadOnlyDictionary<string, int> RunLoot => _run;

		/// <summary>Puts loot in the stash. Returns how many were taken (fewer than asked only when the stash is full).</summary>
		public int AddLoot(string itemId, int quantity)
		{
			var added = Stash.AddUpTo(itemId, quantity);
			if (added > 0)
			{
				_run.TryGetValue(itemId, out var already);
				_run[itemId] = already + added;
			}
			return added;
		}

		public void StartRun() => _run.Clear();

		/// <summary>The dungeon was cleared in <paramref name="seconds"/>. Returns true when that is a new best time.</summary>
		public bool CompleteRun(float seconds)
		{
			Completions++;
			var best = BestSeconds <= 0f || seconds < BestSeconds;
			if (best) BestSeconds = seconds;
			return best;
		}

		public DungeonSave ToSave() => new DungeonSave { stash = Stash.ToData(), completions = Completions, bestSeconds = BestSeconds };

		public void Load(DungeonSave save)
		{
			if (save == null) return;
			Stash.Load(save.stash);
			Completions = Math.Max(0, save.completions);
			BestSeconds = Math.Max(0f, save.bestSeconds);
		}
	}

	/// <summary>Reads and writes the <see cref="DungeonSave"/> in the game's save slot.</summary>
	public sealed class DungeonSaveStore
	{
		const string Slot = "default";
		const int PayloadVersion = 1;

		readonly SaveStore _store;

		public DungeonSaveStore(string rootDirectory = null)
		{
			_store = new SaveStore("dungeons", rootDirectory);
		}

		/// <summary>The saved game, or null when there is none yet or it cannot be read.</summary>
		public DungeonSave Load()
		{
			var result = _store.Load<DungeonSave>(Slot, PayloadVersion);
			return result.Ok ? result.Value : null;
		}

		public void Save(DungeonSave save) => _store.Save(Slot, save, PayloadVersion);
	}
}
