using System.Collections.Generic;
using System.Globalization;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>
	/// Makes the dungeon a co-op game for up to four heroes. The host is the authority: only it runs the monsters (the NavMesh
	/// AI), the boss, drops, who picks up what, and when the run ends. A client's machine shows the monsters it is told about
	/// (entities, ids are the monster's place in the scene's list), and sends what its hero does as requests.
	/// <list type="bullet">
	/// <item>Each hero is simulated by its owner and published as a pose; the others show it with <see cref="RemoteHero"/>.</item>
	/// <item>A client's sword or shockwave hit is an event to the host (monster id and kind of attack). The host checks the
	/// distance and the rate, applies the damage, and the monster's health reaches everybody in its entity.</item>
	/// <item>A monster's hit on a hero: the host decides who is hit and tells that hero's owner (<c>SendTo</c>); the owner
	/// applies it to its own hero.</item>
	/// <item>Drops are entities too. The host sees who walks over one and tells that hero's owner what it got.</item>
	/// <item>A fallen hero stays down and watches. Everybody fallen ends the run in defeat; once the boss is dead and the living
	/// heroes have left through the exit (or after a short wait for the last ones) the host ends it in victory.</item>
	/// </list>
	/// Entity kinds: 1 goblin, 2 skeleton, 3 boss, 10 and up a drop (<see cref="LootItems"/> index plus 10; the value is the amount).
	/// </summary>
	public sealed class DungeonNet : MonoBehaviour
	{
		public const string HitEvent = "dg.hit";
		public const string KillEvent = "dg.kill";
		public const string GateEvent = "dg.gate";
		public const string LootEvent = "dg.loot";
		public const string DamageEvent = "dg.dmg";
		public const string FinishEvent = "dg.finish";
		public const string EndEvent = "dg.end";

		const float PoseEvery = 1f / 15f;
		const float EntitiesEvery = 0.1f;
		const float FinishGrace = 45f;
		const int FirstLootKind = 10;
		const float MonsterHealthPerExtraHero = 0.5f;

		public static readonly string[] LootItems = { Loot.Gold, Loot.BoneCharm, Loot.EmberBlade, Loot.BossSigil, Loot.HealthOrb };

		public static DungeonNet Active { get; private set; }

		/// <summary>True on a client: its hits are requests to the host.</summary>
		public static bool SendsHits => Active != null && Active._net != null && Active._net.IsClient;

		public IReadOnlyDictionary<ulong, RemoteHero> Heroes => _heroes;
		public bool IsHost => _net.IsHost;

		DungeonGame _game;
		NetSession _net;
		PlayerFighter _fighter;
		bool _leaving;
		bool _finishSent;
		float _nextPose;
		float _nextEntities;
		float _firstFinishAt = -1f;
		int _nextLoot = 1000;
		Vector3 _lastPos;
		float _lastPosAt;
		readonly Dictionary<ulong, RemoteHero> _heroes = new Dictionary<ulong, RemoteHero>();
		readonly Dictionary<int, Monster> _monsters = new Dictionary<int, Monster>();
		readonly Dictionary<int, LootPickup> _loot = new Dictionary<int, LootPickup>();
		readonly Dictionary<int, int> _seenAttacks = new Dictionary<int, int>();
		readonly HashSet<int> _goneLoot = new HashSet<int>();
		readonly HashSet<ulong> _finished = new HashSet<ulong>();
		readonly Dictionary<long, float> _lastHit = new Dictionary<long, float>();
		readonly List<NetEntity> _entities = new List<NetEntity>();
		readonly List<int> _pickedUp = new List<int>();

		public DungeonNet Init(DungeonGame game)
		{
			Active = this;
			_game = game;
			_net = NetSession.Instance;
			_fighter = game.PlayerHealth.GetComponent<PlayerFighter>();

			// More heroes, tougher monsters. A single hero is untouched.
			var scale = 1f + MonsterHealthPerExtraHero * Mathf.Max(0, _net.Players.Count - 1);
			for (var i = 0; i < game.StartingMonsters.Length; i++)
			{
				var monster = game.StartingMonsters[i];
				monster.NetId = i + 1;
				_monsters[monster.NetId] = monster;
				if (scale > 1f)
				{
					monster.Health.MaxHealth *= scale;
					monster.Health.Restore();
				}
				if (_net.IsClient)
				{
					monster.MakeProxy();
					monster.gameObject.AddComponent<RemoteBody>();
				}
			}

			_net.On(HitEvent, OnHit);
			_net.On(FinishEvent, OnFinish);
			_net.On(KillEvent, OnKill);
			_net.On(GateEvent, OnGate);
			_net.On(LootEvent, OnLoot);
			_net.On(DamageEvent, OnDamage);
			_net.On(EndEvent, OnEnd);
			_net.Disconnected += OnDisconnected;
			_net.PlayerLeft += OnPlayerLeft;
			_net.EntityUpdated += OnEntity;
			_net.EntityGone += OnGone;
			return this;
		}

		void Start()
		{
			// Heroes enter side by side, in the order of the roster (the host first).
			var players = _net.Players;
			var slot = 0;
			for (var i = 0; i < players.Count; i++) if (players[i].Id == _net.LocalId) slot = i;
			var hero = _game.PlayerHealth.transform;
			var controller = hero.GetComponent<CharacterController>();
			var spot = hero.position + Vector3.right * ((slot - (players.Count - 1) * 0.5f) * 1.4f);
			if (controller != null) controller.enabled = false;
			hero.position = spot;
			if (controller != null) controller.enabled = true;
			_lastPos = spot;
			_lastPosAt = Time.unscaledTime;
		}

		void OnDestroy()
		{
			if (Active == this) Active = null;
			if (_net == null) return;
			_net.Off(HitEvent, OnHit);
			_net.Off(FinishEvent, OnFinish);
			_net.Off(KillEvent, OnKill);
			_net.Off(GateEvent, OnGate);
			_net.Off(LootEvent, OnLoot);
			_net.Off(DamageEvent, OnDamage);
			_net.Off(EndEvent, OnEnd);
			_net.Disconnected -= OnDisconnected;
			_net.PlayerLeft -= OnPlayerLeft;
			_net.EntityUpdated -= OnEntity;
			_net.EntityGone -= OnGone;
		}

		void OnDisconnected(string reason)
		{
			// The host left or the connection dropped: nothing to play together any more.
			if (_leaving) return;
			_leaving = true;
			_game.QuitToMenu();
		}

		void OnPlayerLeft(NetPlayer player)
		{
			if (_heroes.TryGetValue(player.Id, out var hero) && hero != null) Destroy(hero.gameObject);
			_heroes.Remove(player.Id);
			_finished.Remove(player.Id);
		}

		// ---- every machine ----

		void Update()
		{
			if (!_net.IsOnline) return;
			ShowHeroes();

			if (Time.unscaledTime >= _nextPose)
			{
				_nextPose = Time.unscaledTime + PoseEvery;
				_net.PublishPose(MyPose());
			}

			if (!_net.IsHost) return;
			if (Time.unscaledTime >= _nextEntities)
			{
				_nextEntities = Time.unscaledTime + EntitiesEvery;
				PublishEntities();
			}
			HostTick();
		}

		NetPose MyPose()
		{
			var hero = _game.PlayerHealth;
			var position = hero.transform.position;
			var dt = Mathf.Max(0.001f, Time.unscaledTime - _lastPosAt);
			var flat = position - _lastPos;
			flat.y = 0f;
			var speed = flat.magnitude / dt;
			_lastPos = position;
			_lastPosAt = Time.unscaledTime;
			return new NetPose
			{
				Position = position,
				Yaw = hero.transform.eulerAngles.y,
				Speed = speed,
				State = (hero.IsAlive ? 0 : 1) | ((_fighter != null ? _fighter.SwingCount & 7 : 0) << 1) | ((_fighter != null ? _fighter.ShockCount & 7 : 0) << 4),
				A = hero.Current,
				B = hero.MaxHealth,
			};
		}

		void ShowHeroes()
		{
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				if (!_heroes.TryGetValue(player.Id, out var hero) || hero == null)
				{
					hero = RemoteHero.Create(player.Id, player.Name, ShellConfig.Current.ColourOf(player.Colour));
					_heroes[player.Id] = hero;
				}
				if (_net.TryGetPose(player.Id, out var pose)) hero.Show(pose);
			}
		}

		// ---- host: what it simulates ----

		/// <summary>Host: a drop was made; give it an id every machine knows and publish it.</summary>
		public void Register(LootPickup pickup)
		{
			if (pickup == null || !_net.IsHost) return;
			pickup.NetId = _nextLoot++;
			_loot[pickup.NetId] = pickup;
		}

		public void MonsterKilled(Monster monster)
		{
			if (!_net.IsHost) return;
			_net.Send(KillEvent, monster.NetId.ToString(CultureInfo.InvariantCulture));
			_net.RemoveEntity(monster.NetId);
		}

		public void GateOpened(int index)
		{
			if (_net.IsHost) _net.Send(GateEvent, index.ToString(CultureInfo.InvariantCulture));
		}

		void PublishEntities()
		{
			_entities.Clear();
			foreach (var monster in _monsters.Values)
			{
				if (monster == null || !monster.Health.IsAlive || !monster.gameObject.activeInHierarchy) continue;
				_entities.Add(new NetEntity
				{
					Id = monster.NetId,
					Kind = monster.NetKind,
					Position = monster.transform.position,
					Yaw = monster.transform.eulerAngles.y,
					Value = monster.Health.Current,
					State = (monster.IsWindingUp ? 1 : 0) | ((monster.AttackCount & 7) << 2),
				});
			}
			foreach (var pair in _loot)
			{
				var pickup = pair.Value;
				if (pickup == null) continue;
				_entities.Add(new NetEntity
				{
					Id = pair.Key,
					Kind = FirstLootKind + System.Array.IndexOf(LootItems, pickup.ItemId),
					Position = pickup.transform.position,
					Value = pickup.Quantity,
				});
			}
			_net.PublishEntities(_entities);
		}

		/// <summary>Host: a monster hit a hero of another player; that player's machine applies it.</summary>
		public void DamageHero(RemoteHero hero, float amount, string type)
		{
			if (!_net.IsHost || hero == null || hero.Dead) return;
			_net.SendTo(hero.Owner, DamageEvent, amount.ToString("R", CultureInfo.InvariantCulture) + "|" + type);
		}

		/// <summary>Host: the boss's slam lands; every other player's hero inside the circle is hurt.</summary>
		public void SlamHeroes(Vector3 center, float radius, float damage)
		{
			if (!_net.IsHost) return;
			foreach (var hero in _heroes.Values)
			{
				if (hero == null || hero.Dead) continue;
				if (Flat(hero.transform.position - center).magnitude <= radius) DamageHero(hero, damage, "slam");
			}
		}

		void OnHit(ulong sender, string payload)
		{
			if (!_net.IsHost || _game.Phase != DungeonPhase.Playing) return;
			var parts = payload.Split('|');
			if (parts.Length != 2 || !int.TryParse(parts[0], out var id)) return;
			var melee = parts[1] == "melee";
			if (!melee && parts[1] != "shockwave") return;
			if (!_heroes.TryGetValue(sender, out var hero) || hero == null || hero.Dead) return;
			if (!_monsters.TryGetValue(id, out var monster) || !monster.Health.IsAlive || !monster.gameObject.activeInHierarchy) return;

			// Reachable from where that hero is (with some room for the delay), and not faster than the weapon allows.
			var reach = melee ? _fighter.AttackRange + 2f : _fighter.AbilityRadius + 2.5f;
			if (Flat(monster.transform.position - hero.transform.position).magnitude > reach + 1.2f) return;
			var key2 = unchecked((long)sender * 100003L + id) * 2L + (melee ? 0L : 1L);
			var minGap = melee ? _fighter.AttackCooldown * 0.65f : _fighter.AbilityCooldown * 0.66f;
			if (_lastHit.TryGetValue(key2, out var last) && Time.time - last < minGap) return;
			_lastHit[key2] = Time.time;

			var amount = melee ? _fighter.AttackDamage : _fighter.AbilityDamage;
			monster.Health.ApplyDamage(new DamageInfo(amount, hero.gameObject, monster.transform.position, melee ? "melee" : "shockwave"));
		}

		void HostTick()
		{
			if (_game.Phase != DungeonPhase.Playing) return;
			PickUps();
			CheckEnd();
		}

		void PickUps()
		{
			_pickedUp.Clear();
			var local = _game.PlayerHealth;
			foreach (var pair in _loot)
			{
				var pickup = pair.Value;
				if (pickup == null)
				{
					_pickedUp.Add(pair.Key);
					continue;
				}
				var at = pickup.transform.position;
				if (local.IsAlive && Near(local.transform.position, at) && Wants(pickup, local.Current, local.MaxHealth))
				{
					_game.Collect(pickup.ItemId, pickup.Quantity);
					_pickedUp.Add(pair.Key);
					Destroy(pickup.gameObject);
					continue;
				}
				foreach (var hero in _heroes.Values)
				{
					if (hero == null || hero.Dead || !Near(hero.transform.position, at) || !Wants(pickup, hero.Health, hero.MaxHealth)) continue;
					_net.SendTo(hero.Owner, LootEvent, pickup.ItemId + "|" + pickup.Quantity.ToString(CultureInfo.InvariantCulture));
					_pickedUp.Add(pair.Key);
					Destroy(pickup.gameObject);
					break;
				}
			}
			foreach (var id in _pickedUp)
			{
				_loot.Remove(id);
				_net.RemoveEntity(id);
			}
		}

		static bool Near(Vector3 hero, Vector3 pickup) => Flat(hero - pickup).magnitude <= 1.6f;

		// A heal orb is kept for someone who needs it.
		static bool Wants(LootPickup pickup, float health, float max) => pickup.ItemId != Loot.HealthOrb || health < max - 0.01f;

		void CheckEnd()
		{
			var anyAlive = _game.PlayerHealth.IsAlive;
			var allLivingFinished = !_game.PlayerHealth.IsAlive || _finished.Contains(_net.LocalId);
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				var dead = _heroes.TryGetValue(player.Id, out var hero) && hero != null && hero.Dead;
				if (dead) continue;
				anyAlive = true;
				if (!_finished.Contains(player.Id)) allLivingFinished = false;
			}

			if (!anyAlive)
			{
				Announce(false);
				return;
			}
			if (!_game.BossDead || _finished.Count == 0) return;
			if (allLivingFinished || Time.time - _firstFinishAt >= FinishGrace) Announce(true);
		}

		/// <summary>Host: ends the run for everybody.</summary>
		public void Announce(bool victory)
		{
			if (!_net.IsHost || _game.Phase != DungeonPhase.Playing) return;
			_net.Send(EndEvent, victory ? "victory" : "defeat");
			_game.EndRun(victory);
		}

		/// <summary>This hero stepped into the exit after opening the chest.</summary>
		public void Finish()
		{
			if (_finishSent) return;
			_finishSent = true;
			if (_net.IsHost) MarkFinished(_net.LocalId);
			else _net.Send(FinishEvent, "", true);
		}

		void OnFinish(ulong sender, string payload)
		{
			if (_net.IsHost && _game.BossDead) MarkFinished(sender);
		}

		void MarkFinished(ulong player)
		{
			if (_finished.Count == 0) _firstFinishAt = Time.time;
			_finished.Add(player);
		}

		// ---- client: what the host tells ----

		void OnEntity(NetEntity entity)
		{
			if (entity.Kind < FirstLootKind)
			{
				if (!_monsters.TryGetValue(entity.Id, out var monster) || !monster.Proxy || !monster.Health.IsAlive) return;
				var body = monster.GetComponent<RemoteBody>();
				if (body != null) body.Apply(entity);

				var drop = monster.Health.Current - entity.Value;
				if (entity.Value > 0f && drop > 0.01f) monster.Health.ApplyDamage(new DamageInfo(drop));

				monster.ShowProxySlam((entity.State & 1) != 0);
				var attacks = (entity.State >> 2) & 7;
				if (_seenAttacks.TryGetValue(entity.Id, out var before) && before != attacks) monster.PlayLunge();
				_seenAttacks[entity.Id] = attacks;
				return;
			}

			if (_loot.ContainsKey(entity.Id) || _goneLoot.Contains(entity.Id)) return;
			var index = entity.Kind - FirstLootKind;
			if (index < 0 || index >= LootItems.Length) return;
			var pickup = LootPickup.Spawn(LootItems[index], Mathf.RoundToInt(entity.Value), entity.Position);
			pickup.NetId = entity.Id;
			pickup.Proxy = true;
			_loot[entity.Id] = pickup;
		}

		void OnGone(int id)
		{
			if (!_loot.TryGetValue(id, out var pickup))
			{
				if (id >= 1000) _goneLoot.Add(id); // the removal came before the drop was ever shown
				return;
			}
			_loot.Remove(id);
			_goneLoot.Add(id);
			if (pickup != null) Destroy(pickup.gameObject);
		}

		void OnKill(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL || !int.TryParse(payload, out var id)) return;
			if (!_monsters.TryGetValue(id, out var monster) || !monster.Health.IsAlive) return;
			var body = monster.GetComponent<RemoteBody>();
			if (body != null) Destroy(body); // it falls now; nothing eases it upright
			monster.Health.ApplyDamage(new DamageInfo(1000000f));
			_game.CountKill();
			if (monster.IsBoss) _game.BossFell(monster.transform.position);
		}

		void OnGate(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL || !int.TryParse(payload, out var index)) return;
			if (index < 0 || index >= _game.Gates.Length || _game.Gates[index].IsOpen) return;
			_game.Gates[index].Open();
			_game.ShowToast("A door opens.");
		}

		void OnLoot(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL) return;
			var parts = payload.Split('|');
			if (parts.Length == 2 && int.TryParse(parts[1], out var quantity)) _game.Collect(parts[0], quantity);
		}

		void OnDamage(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL || _game.Phase != DungeonPhase.Playing) return;
			var parts = payload.Split('|');
			if (parts.Length != 2 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)) return;
			var hero = _game.PlayerHealth;
			hero.ApplyDamage(new DamageInfo(amount, null, hero.transform.position, parts[1]));
		}

		void OnEnd(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL) return;
			_game.EndRun(payload == "victory");
		}

		static Vector3 Flat(Vector3 v)
		{
			v.y = 0f;
			return v;
		}

		/// <summary>Client: asks the host to hurt a monster.</summary>
		public void SendHit(Health target, string type)
		{
			if (target == null) return;
			var monster = target.GetComponentInParent<Monster>();
			if (monster == null || monster.NetId == 0) return;
			_net.Send(HitEvent, monster.NetId.ToString(CultureInfo.InvariantCulture) + "|" + type, true);
		}
	}
}
