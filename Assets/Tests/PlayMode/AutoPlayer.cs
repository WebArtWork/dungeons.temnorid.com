using UnityEngine;
using UnityEngine.AI;

namespace Temnorid.Dungeons.Tests
{
	/// <summary>
	/// A test-only player that fights the dungeon the way a decent person would, so the difficulty can be measured
	/// instead of guessed: walk to the nearest monster, swing, use the shockwave when it pays, take the loot and the exit.
	/// A "smart" bot also steps out of the boss's slam after a human-sized reaction time; a basic one never does.
	/// </summary>
	public sealed class AutoPlayer : MonoBehaviour
	{
		public bool Dodges;
		public float Speed = 5f;
		public float ReactionSeconds = 0.4f;

		DungeonGame _game;
		PlayerFighter _fighter;
		CharacterController _controller;
		float _windupSeenAt = -1f;
		NavMeshPath _path;
		float _repathAt;
		Vector3 _corner;

		public int Swings { get; private set; }

		void Start()
		{
			_game = DungeonGame.Instance;
			_fighter = GetComponent<PlayerFighter>();
			_controller = GetComponent<CharacterController>();
			_path = new NavMeshPath(); // not allowed in a field initializer
		}

		void Update()
		{
			if (_game == null || _game.Phase != DungeonPhase.Playing || !_fighter.Health.IsAlive) return;
			var position = transform.position;

			// Step out of the slam once the red warning has been seen for a moment.
			Monster windup = null;
			foreach (var monster in Monster.All) if (monster.IsWindingUp) windup = monster;
			if (windup == null) _windupSeenAt = -1f;
			else if (Dodges)
			{
				if (_windupSeenAt < 0f) _windupSeenAt = Time.time;
				var away = Flat(position - windup.SlamCenter);
				if (Time.time - _windupSeenAt >= ReactionSeconds && away.magnitude < windup.SlamRadius + 0.8f)
				{
					Walk(away.sqrMagnitude < 0.01f ? Vector3.back : away.normalized);
					return;
				}
			}

			// The nearest monster still alive.
			Monster target = null;
			var best = float.MaxValue;
			var nearby = 0;
			foreach (var monster in Monster.All)
			{
				var distance = Flat(monster.transform.position - position).magnitude;
				if (distance < 5.5f) nearby++;
				if (distance < best)
				{
					best = distance;
					target = monster;
				}
			}

			if (target != null)
			{
				var direction = Flat(target.transform.position - position);
				if (best > 2.2f)
				{
					WalkTo(target.transform.position);
					return;
				}
				_fighter.AimOverride = direction.normalized;
				if (_fighter.TrySwing()) Swings++;
				if (nearby >= 2 || target.IsBoss) _fighter.TryShockwave();
				return;
			}

			// Everything is dead: the chest, then the way out.
			if (_game.Chest.CanOpen)
			{
				var toChest = Flat(_game.Chest.transform.position - position);
				if (toChest.magnitude > 2.0f) WalkTo(_game.Chest.transform.position);
				else _game.OpenChest();
				return;
			}
			if (_game.Exit.IsOpen) WalkTo(_game.Exit.transform.position);
		}

		/// <summary>Walks along the NavMesh path to a point, as a person finds their way round corners.</summary>
		void WalkTo(Vector3 destination)
		{
			if (Time.time >= _repathAt)
			{
				_repathAt = Time.time + 0.25f;
				_corner = destination;
				if (NavMesh.SamplePosition(transform.position, out var from, 3f, NavMesh.AllAreas)
					&& NavMesh.SamplePosition(destination, out var to, 3f, NavMesh.AllAreas)
					&& NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path)
					&& _path.corners.Length > 1)
				{
					_corner = _path.corners[1];
					if (Flat(_corner - transform.position).magnitude < 0.6f && _path.corners.Length > 2) _corner = _path.corners[2];
				}
			}
			var heading = Flat(_corner - transform.position);
			if (heading.sqrMagnitude > 0.01f) Walk(heading.normalized);
		}

		void Walk(Vector3 direction)
		{
			_controller.Move(direction * (Speed * Time.deltaTime) + Vector3.down * (2f * Time.deltaTime));
		}

		static Vector3 Flat(Vector3 v)
		{
			v.y = 0f;
			return v;
		}
	}
}
