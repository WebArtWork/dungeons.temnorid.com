using System.IO;
using NUnit.Framework;

namespace Temnorid.Dungeons.Tests
{
	public class DungeonSessionTests
	{
		[Test]
		public void Loot_goes_to_the_stash_and_is_counted_for_the_run()
		{
			var session = new DungeonSession();
			session.StartRun();
			Assert.AreEqual(5, session.AddLoot(Loot.Gold, 5));
			Assert.AreEqual(1, session.AddLoot(Loot.BoneCharm, 1));
			Assert.AreEqual(10, session.AddLoot(Loot.Gold, 10));

			Assert.AreEqual(15, session.Stash.Count(Loot.Gold));
			Assert.AreEqual(15, session.RunLoot[Loot.Gold]);
			Assert.AreEqual(1, session.RunLoot[Loot.BoneCharm]);
		}

		[Test]
		public void The_stash_keeps_everything_but_each_run_counts_its_own_loot()
		{
			var session = new DungeonSession();
			session.AddLoot(Loot.Gold, 20);
			session.StartRun();
			Assert.AreEqual(0, session.RunLoot.Count);
			session.AddLoot(Loot.Gold, 5);

			Assert.AreEqual(25, session.Stash.Count(Loot.Gold));
			Assert.AreEqual(5, session.RunLoot[Loot.Gold]);
		}

		[Test]
		public void A_full_stash_takes_only_what_fits()
		{
			var session = new DungeonSession();
			for (var i = 0; i < DungeonSession.StashSlots; i++) session.AddLoot("item-" + i, DungeonSession.StashStack);
			Assert.AreEqual(0, session.AddLoot(Loot.Gold, 5), "no free slot for a new kind");
			Assert.IsFalse(session.RunLoot.ContainsKey(Loot.Gold), "and it is not counted as looted");
		}

		[Test]
		public void Completing_runs_counts_them_and_keeps_the_best_time()
		{
			var session = new DungeonSession();
			Assert.IsTrue(session.CompleteRun(120f), "the first run is a best");
			Assert.IsFalse(session.CompleteRun(150f));
			Assert.IsTrue(session.CompleteRun(95.5f));
			Assert.AreEqual(3, session.Completions);
			Assert.AreEqual(95.5f, session.BestSeconds, 0.001f);
		}

		[Test]
		public void Progress_survives_a_restart_and_a_damaged_save_starts_fresh()
		{
			var folder = Path.Combine(Path.GetTempPath(), "dungeons-test-" + System.Guid.NewGuid());
			try
			{
				var store = new DungeonSaveStore(folder);
				Assert.IsNull(store.Load(), "nothing saved yet");

				var session = new DungeonSession();
				session.AddLoot(Loot.EmberBlade, 1);
				session.AddLoot(Loot.Gold, 55);
				session.CompleteRun(88f);
				store.Save(session.ToSave());

				var restored = new DungeonSession();
				restored.Load(new DungeonSaveStore(folder).Load());
				Assert.AreEqual(1, restored.Stash.Count(Loot.EmberBlade));
				Assert.AreEqual(55, restored.Stash.Count(Loot.Gold));
				Assert.AreEqual(1, restored.Completions);
				Assert.AreEqual(88f, restored.BestSeconds, 0.001f);

				File.WriteAllText(Path.Combine(folder, "dungeons", "default.json"), "{{{ broken");
				Assert.IsNull(new DungeonSaveStore(folder).Load(), "damaged file: start fresh");
				Assert.AreEqual(1, Directory.GetFiles(Path.Combine(folder, "dungeons"), "default.corrupt-*.json").Length, "the damaged file is kept");
			}
			finally
			{
				if (Directory.Exists(folder)) Directory.Delete(folder, true);
			}
		}
	}
}
