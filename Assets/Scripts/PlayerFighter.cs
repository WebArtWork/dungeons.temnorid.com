using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Input;
using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>
	/// The player's fighting: a sword swing on the main action (left mouse), a shockwave on the second
	/// (right mouse), and opening the chest with E. The hits come from the shared combat package; this class
	/// chooses the numbers and draws the swing and the shockwave.
	/// </summary>
	[RequireComponent(typeof(Health))]
	public sealed class PlayerFighter : MonoBehaviour
	{
		public DungeonGame Game;

		[Header("Sword (main action)")]
		public float AttackRange = 3.2f;
		public float AttackHalfAngle = 75f;
		public float AttackDamage = 18f;
		public float AttackCooldown = 0.55f;

		[Header("Shockwave (second action)")]
		public float AbilityRadius = 5.5f;
		public float AbilityDamage = 35f;
		public float AbilityCooldown = 6f;

		public Health Health { get; private set; }

		/// <summary>What the player can do right now, for the screen.</summary>
		public string Prompt { get; private set; } = "";

		/// <summary>0 while the shockwave is recharging, 1 when it is ready.</summary>
		public float AbilityReady => Mathf.Clamp01(1f - (_nextAbility - Time.time) / AbilityCooldown);

		/// <summary>Counts swings and shockwaves, so other players' machines can show them.</summary>
		public int SwingCount { get; private set; }
		public int ShockCount { get; private set; }

		float _nextAttack;
		float _nextAbility;
		bool _wasInteractDown;

		void Awake()
		{
			Health = GetComponent<Health>();
		}

		void Update()
		{
			if (Game == null || Game.Phase != DungeonPhase.Playing || Game.Paused || !Health.IsAlive)
			{
				Prompt = "";
				return;
			}

			var actions = EngineInput.Default.Player;

			var down = actions.Interact.IsPressed();
			var pressedNow = down && !_wasInteractDown;
			_wasInteractDown = down;

			Prompt = "";
			var chest = Game.Chest;
			if (chest != null && chest.CanOpen && chest.DistanceFrom(transform.position) <= 2.8f)
			{
				Prompt = Application.isMobilePlatform ? "Tap Use to open the chest" : "Press E to open the chest";
				if (pressedNow) Game.OpenChest();
			}

			if (actions.Primary.IsPressed()) TrySwing();
			if (actions.Secondary.IsPressed()) TryShockwave();
		}

		/// <summary>Aim here instead of where the camera looks. Used by automated tests that play the game.</summary>
		public Vector3? AimOverride;

		/// <summary>Swings the sword if it is ready. Returns whether it swung.</summary>
		public bool TrySwing()
		{
			if (Time.time < _nextAttack) return false;
			Swing();
			return true;
		}

		/// <summary>Sends out the shockwave if it is ready. Returns whether it fired.</summary>
		public bool TryShockwave()
		{
			if (Time.time < _nextAbility) return false;
			Shockwave();
			return true;
		}

		Vector3 AimDirection()
		{
			if (AimOverride.HasValue) return AimOverride.Value;
			var camera = Camera.main;
			var forward = camera != null ? camera.transform.forward : transform.forward;
			forward.y = 0f;
			return forward.sqrMagnitude > 0.001f ? forward.normalized : transform.forward;
		}

		void Swing()
		{
			_nextAttack = Time.time + AttackCooldown;
			SwingCount++;
			var direction = AimDirection();
			var origin = transform.position;
			if (DungeonNet.SendsHits)
			{
				// A client's hit is a request: the host checks it and applies the damage.
				foreach (var target in CombatQueries.FindInCone(origin, direction, AttackRange, AttackHalfAngle, Health.Team))
					DungeonNet.Active.SendHit(target, "melee");
			}
			else CombatQueries.DamageInCone(origin, direction, AttackRange, AttackHalfAngle, Health.Team, new DamageInfo(AttackDamage, gameObject, origin, "melee"));

			var slashPosition = origin + Vector3.up * 1.1f + direction * 1.5f;
			Fx.Pulse(PrimitiveType.Cube, slashPosition, Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, 30f),
				new Vector3(0.3f, 0.06f, 0.5f), new Vector3(2.8f, 0.06f, 0.5f), new Color(1f, 0.95f, 0.7f), 0.14f);
		}

		void Shockwave()
		{
			_nextAbility = Time.time + AbilityCooldown;
			ShockCount++;
			var origin = transform.position;
			if (DungeonNet.SendsHits)
			{
				foreach (var target in CombatQueries.FindInRadius(origin, AbilityRadius, Health.Team))
					DungeonNet.Active.SendHit(target, "shockwave");
			}
			else CombatQueries.DamageInRadius(origin, AbilityRadius, Health.Team, new DamageInfo(AbilityDamage, gameObject, origin, "shockwave"));
			Fx.Pulse(PrimitiveType.Cylinder, new Vector3(origin.x, 0.08f, origin.z), Quaternion.identity,
				new Vector3(1f, 0.03f, 1f), new Vector3(AbilityRadius * 2f, 0.03f, AbilityRadius * 2f), new Color(0.3f, 0.85f, 1f), 0.35f);
		}
	}
}
