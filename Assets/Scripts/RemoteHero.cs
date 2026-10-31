using ITKamianets.Engine.AI;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.UI;
using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>
	/// Another player's hero, shown on this machine from the poses its owner publishes. It has no health of its own here: the
	/// owner applies damage to the real hero and says so in its pose. On the host it is also something monsters hunt (an
	/// <see cref="AiTarget"/>), so the host decides who they chase and tells the owner when a hero is hit.
	/// </summary>
	public sealed class RemoteHero : MonoBehaviour
	{
		public ulong Owner;
		public string DisplayName = "Hero";
		public bool Dead { get; private set; }
		public float Health { get; private set; } = 180f;
		public float MaxHealth { get; private set; } = 180f;

		RemoteBody _body;
		AiTarget _marker;
		Transform _visual;
		TextMesh _name;
		int _swings = -1;
		int _shocks = -1;

		public static RemoteHero Create(ulong owner, string displayName, Color tint)
		{
			var go = new GameObject("Hero_" + displayName);
			var hero = go.AddComponent<RemoteHero>();
			hero.Owner = owner;
			hero.DisplayName = displayName;
			hero.Build(tint);
			return hero;
		}

		void Build(Color tint)
		{
			_body = gameObject.AddComponent<RemoteBody>();
			_marker = gameObject.AddComponent<AiTarget>();
			_marker.Team = 1;
			var trigger = gameObject.AddComponent<CapsuleCollider>();
			trigger.isTrigger = true; // monsters see through it and nothing collides with it
			trigger.radius = 0.4f;
			trigger.height = 1.8f;
			trigger.center = new Vector3(0f, 0.9f, 0f);

			_visual = new GameObject("Body").transform;
			_visual.SetParent(transform, false);
			var steel = Fx.Material(new Color(0.75f, 0.78f, 0.85f));
			var skin = Fx.Material(new Color(0.88f, 0.7f, 0.55f));
			Part(PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), Fx.Material(tint));
			Part(PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), skin);
			Part(PrimitiveType.Cube, new Vector3(0.5f, 1.0f, 0.45f), new Vector3(0.08f, 0.08f, 1.1f), steel);

			var font = new UiBuilder().Font;
			var label = new GameObject("Name");
			label.transform.SetParent(transform, false);
			label.transform.localPosition = new Vector3(0f, 2.3f, 0f);
			_name = label.AddComponent<TextMesh>();
			_name.text = DisplayName;
			_name.font = font;
			_name.fontSize = 48;
			_name.characterSize = 0.06f;
			_name.anchor = TextAnchor.MiddleCenter;
			_name.color = tint;
			var renderer = label.GetComponent<MeshRenderer>();
			if (font != null) renderer.sharedMaterial = font.material;
		}

		void Part(PrimitiveType type, Vector3 position, Vector3 scale, Material material)
		{
			var part = GameObject.CreatePrimitive(type);
			Destroy(part.GetComponent<Collider>());
			part.transform.SetParent(_visual, false);
			part.transform.localPosition = position;
			part.transform.localScale = scale;
			part.GetComponent<Renderer>().sharedMaterial = material;
		}

		/// <summary>Shows the newest pose. State: bit 0 fallen, bits 1 to 3 swing counter, bits 4 to 6 shockwave counter.</summary>
		public void Show(NetPose pose)
		{
			_body.Apply(pose);
			Health = pose.A;
			MaxHealth = pose.B > 0f ? pose.B : MaxHealth;

			var dead = (pose.State & 1) != 0;
			if (dead != Dead)
			{
				Dead = dead;
				_marker.Alive = !dead;
				_visual.localRotation = dead ? Quaternion.Euler(-80f, 0f, 0f) : Quaternion.identity;
			}

			var swings = (pose.State >> 1) & 7;
			var shocks = (pose.State >> 4) & 7;
			if (_swings >= 0 && swings != _swings)
			{
				var forward = transform.forward;
				Fx.Pulse(PrimitiveType.Cube, transform.position + Vector3.up * 1.1f + forward * 1.5f, Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, 30f),
					new Vector3(0.3f, 0.06f, 0.5f), new Vector3(2.8f, 0.06f, 0.5f), new Color(1f, 0.95f, 0.7f), 0.14f);
			}
			if (_shocks >= 0 && shocks != _shocks)
			{
				var origin = transform.position;
				Fx.Pulse(PrimitiveType.Cylinder, new Vector3(origin.x, 0.08f, origin.z), Quaternion.identity,
					new Vector3(1f, 0.03f, 1f), new Vector3(11f, 0.03f, 11f), new Color(0.3f, 0.85f, 1f), 0.35f);
			}
			_swings = swings;
			_shocks = shocks;
		}

		void LateUpdate()
		{
			var camera = Camera.main;
			if (_name != null && camera != null) _name.transform.rotation = camera.transform.rotation;
		}
	}
}
