using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITKamianets.Engine.Account;
using Temnorid.Village;

namespace Temnorid.Dungeons
{
	/// <summary>Which dungeon this trip is to. The arena sets it; the dungeon scene reads it.</summary>
	public static class DungeonSelection
	{
		public const string Default = "bakota-caves";

		public static string Id = Default;

		public static DungeonDef Current => DungeonCatalog.ById(Id) ?? DungeonCatalog.ById(Default);
	}

	/// <summary>
	/// The lord's hero, shared with Lords. A Google player's hero is the one in <c>users/{uid}/progress/lords</c>: sending it to a dungeon
	/// here writes the same trip Lords writes (so Lords shows the hero away), and finishing the dungeon here ends that trip (so Lords shows the hero
	/// back with the loot, or in the infirmary). A guest or an offline player has a hero of their own that lives only while the game is open.
	/// </summary>
	public sealed class HeroLink
	{
		/// <summary>The hero of a guest: kept for the run of the game, not saved.</summary>
		static HeroSave _guestHero;

		/// <summary>The write that ends a trip, while it is still running: the arena waits for it before it reads the hero.</summary>
		public static Task Pending;

		readonly IProgressStore _store;
		string _updateTime = "";

		public HeroSave Hero { get; private set; } = HeroSave.New(Now());
		public IDictionary<string, VillageBuilding> Buildings { get; private set; } = new Dictionary<string, VillageBuilding>();

		/// <summary>True when this is the lord's own hero in Lords; false for a guest, signed out or offline.</summary>
		public bool IsLords { get; private set; }

		/// <summary>One line on where the hero comes from, for the board.</summary>
		public string Status { get; private set; } = "";

		public HeroLink(IProgressStore store = null)
		{
			_store = store ?? (GameAccount.IsSignedIn && !GameAccount.IsGuest ? FirestoreProgressStore.ForAccount() : null);
		}

		public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		/// <summary>The shared link of this run of the game, created on first use.</summary>
		public static HeroLink Current => _current ?? (_current = new HeroLink());

		static HeroLink _current;

		/// <summary>Forgets the shared link (after signing in or out).</summary>
		public static void Reset() => _current = null;

		/// <summary>Reads the hero fresh from Lords (or the guest's own).</summary>
		public async Task LoadAsync()
		{
			if (_store == null)
			{
				if (_guestHero == null) _guestHero = HeroSave.New(Now());
				Hero = _guestHero;
				IsLords = false;
				Status = GameAccount.IsSignedIn ? "Guest hero: sign in with Google to share your hero with Lords." : "Not signed in: this hero lives only while the game is open.";
				return;
			}

			ProgressDocument document;
			try { document = await _store.LoadAsync(); }
			catch (Exception) { document = ProgressDocument.Failure("Could not read Lords."); }

			if (document.Failed || !document.Exists || document.Progress == null)
			{
				// Lords could not be read: the player still has a hero here, but it is not linked.
				if (_guestHero == null) _guestHero = HeroSave.New(Now());
				Hero = _guestHero;
				IsLords = false;
				Status = document.Failed ? "Could not reach Lords: this hero is not linked." : "No Lords save yet: open Lords once to create your hero.";
				return;
			}

			IsLords = true;
			_updateTime = document.UpdateTime;
			Hero = document.Progress.Hero ?? HeroSave.New(Now());
			Buildings = document.Progress.Village != null ? (IDictionary<string, VillageBuilding>)document.Progress.Village.Buildings : new Dictionary<string, VillageBuilding>();
			Status = "Linked to Lords.";
		}

		public HeroStatus StatusNow => HeroRules.StatusNow(Hero, Now(), Buildings);

		public double HealthNow => HeroRules.HealthNow(Hero, Now(), Buildings);

		public CannotGo WhyNot(DungeonDef dungeon) => HeroRules.WhyNot(Hero, dungeon, Now(), Buildings);

		/// <summary>Sends the hero to <paramref name="dungeon"/>: writes the trip into Lords. Returns an empty string when it worked, else the reason.</summary>
		public async Task<string> SendAsync(DungeonDef dungeon)
		{
			await LoadAsync(); // the trip is judged on what Lords says now, not on what was read a while ago
			var why = WhyNot(dungeon);
			if (why != CannotGo.None) return Reason(why);

			var sent = HeroRules.Send(Hero, dungeon, Now(), Buildings, new Random());
			var error = await WriteAsync(sent);
			if (error.Length > 0) return error;
			DungeonSelection.Id = dungeon.Id;
			return "";
		}

		/// <summary>
		/// The hero is out of the dungeon: the trip ends now, so Lords shows the hero back with the loot, or, when the hero fell, in the infirmary.
		/// Best effort: when Lords cannot be reached the trip simply runs out its time there.
		/// </summary>
		public async Task EndTripAsync(bool fell)
		{
			await LoadAsync();
			if (Hero.Expedition == null) return;
			await WriteAsync(HeroRules.EndTripNow(Hero, Now(), fell));
		}

		async Task<string> WriteAsync(HeroSave hero)
		{
			if (!IsLords)
			{
				_guestHero = hero;
				Hero = hero;
				return "";
			}

			for (var attempt = 0; attempt < 2; attempt++)
			{
				StockWriteResult result;
				try { result = await _store.SaveHeroAsync(hero.ToJson(), _updateTime); }
				catch (Exception) { return "Could not reach Lords."; }

				if (result == StockWriteResult.Saved)
				{
					Hero = hero;
					return "";
				}
				if (result == StockWriteResult.Failed) return "Could not reach Lords.";

				// Lords changed the save in the meantime: read it again and retry once, if the hero can still do this.
				var trip = hero.Expedition;
				await LoadAsync();
				if (trip != null && StatusNow != HeroStatus.Home && Hero.Expedition?.Dungeon != trip.Dungeon) return "The hero is busy in Lords.";
			}
			return "Lords changed while saving: try again.";
		}

		public static string Reason(CannotGo why)
		{
			switch (why)
			{
				case CannotGo.Busy: return "The hero is already away or has loot to hand over in Lords.";
				case CannotGo.Infirmary: return "The hero is in the infirmary.";
				case CannotGo.Weak: return "The hero is too weak for this dungeon.";
				default: return "";
			}
		}
	}
}
