using ITKamianets.Engine.Feedback;
using System;
using System.Collections;
using System.Collections.Generic;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.Combat;
using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>
	/// A monster of the dungeon, built on the shared bot and combat packages: <see cref="AiAgent"/> finds and
	/// chases the player and asks for attacks, <see cref="Health"/> keeps its hit points. This class says what an
	/// attack does, what the monster drops, and, for the boss, the slam with its warning on the floor.
	/// </summary>
	[RequireComponent(typeof(AiAgent), typeof(Health))]
	public sealed class Monster : MonoBehaviour
	{
		public static readonly List<Monster> All = new List<Monster>();

		/// <summary>Raised when a monster dies.</summary>
		public static event Action<Monster> Killed;

		public string DisplayName = "Goblin";
		/// <summary>Which room it belongs to: its gate opens when every monster of the room is dead.</summary>
		public int Room;
		public bool IsBoss;
		public float MeleeDamage = 8f;
		public string LootId;
		public int LootQuantity;
		/// <summary>Health the player recovers from the orb this monster drops, 0 for none.</summary>
		public float HealDrop;

		[Header("Slam (zero damage means no slam)")]
		public float SlamDamage;
		public float SlamRadius = 5.5f;
		public float SlamInterval = 6f;
		public float SlamTelegraphSeconds = 1.3f;
		public float SlamTriggerDistance = 8f;

		public Health Health { get; private set; }
		public AiAgent Agent { get; private set; }

		/// <summary>True while the slam's warning is showing on the floor.</summary>
		public bool IsWindingUp => _slamming;

		/// <summary>Where the slam will land, while <see cref="IsWindingUp"/>.</summary>
		public Vector3 SlamCenter { get; private set; }

		AiTarget _marker;
		float _nextSlam;
		bool _slamming;
		GameObject _warning;

		void Awake()
		{
			Health = GetComponent<Health>();
			Agent = GetComponent<AiAgent>();
			_marker = GetComponent<AiTarget>();
			Agent.AttackRequested += OnAttackRequested;
			Health.Died += OnDied;
			_nextSlam = Time.time + SlamInterval * 0.5f;
		}

		void OnEnable() => All.Add(this);
		void OnDisable() => All.Remove(this);

		void Update()
		{
			if (SlamDamage <= 0f || _slamming || !Health.IsAlive) return;
			if (Time.time < _nextSlam) return;
			if (Agent.State != AiState.Chase && Agent.State != AiState.Attack) return;
			if (Agent.Target == null) return;
			var distance = Vector3.Distance(Agent.Target.position, transform.position);
			if (distance <= SlamTriggerDistance) StartCoroutine(Slam());
		}

		void OnAttackRequested(AiAgent agent, Transform target)
		{
			if (_slamming) return;
			var victim = target.GetComponentInParent<Health>();
			if (victim == null) return;
			victim.ApplyDamage(new DamageInfo(MeleeDamage, gameObject, target.position, "melee"));
			Fx.Pulse(PrimitiveType.Sphere, target.position + Vector3.up * 1f, Quaternion.identity, Vector3.one * 0.2f, Vector3.one * 0.9f, new Color(1f, 0.5f, 0.2f), 0.15f);
		}

		IEnumerator Slam()
		{
			_slamming = true;
			Agent.enabled = false;
			Agent.Nav.isStopped = true;

			// The warning: a red disc on the floor grows to the size of the blow, so the player can see where it lands.
			var center = transform.position;
			SlamCenter = center;
			_warning = Fx.Pulse(PrimitiveType.Cylinder, new Vector3(center.x, 0.05f, center.z), Quaternion.identity,
				new Vector3(0.5f, 0.02f, 0.5f), new Vector3(SlamRadius * 2f, 0.02f, SlamRadius * 2f), new Color(0.95f, 0.1f, 0.05f), SlamTelegraphSeconds);

			yield return new WaitForSeconds(SlamTelegraphSeconds);

			CombatQueries.DamageInRadius(center, SlamRadius, Health.Team, new DamageInfo(SlamDamage, gameObject, center, "slam"));
			Fx.Pulse(PrimitiveType.Cylinder, new Vector3(center.x, 0.1f, center.z), Quaternion.identity,
				new Vector3(SlamRadius * 2f, 0.1f, SlamRadius * 2f), new Vector3(SlamRadius * 2.4f, 0.02f, SlamRadius * 2.4f), new Color(1f, 0.85f, 0.4f), 0.25f);

			_nextSlam = Time.time + SlamInterval;
			Agent.enabled = true;
			Agent.Nav.isStopped = false;
			_slamming = false;
		}

		void OnDied(Health health, DamageInfo info)
		{
			StopAllCoroutines();
			if (_warning != null) Destroy(_warning);
			All.Remove(this);

			Agent.Stop();
			Agent.Nav.enabled = false; // or the agent keeps steering the body while it falls
			if (_marker != null) _marker.Alive = false;
			foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;

			Killed?.Invoke(this);
			StartCoroutine(Fall());
		}

		IEnumerator Fall()
		{
			var start = transform.rotation;
			var end = start * Quaternion.Euler(90f, 0f, 0f);
			for (var t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
			{
				transform.rotation = Quaternion.Slerp(start, end, t);
				yield return null;
			}
			yield return new WaitForSeconds(2f);
			Destroy(gameObject);
		}
	}
}
