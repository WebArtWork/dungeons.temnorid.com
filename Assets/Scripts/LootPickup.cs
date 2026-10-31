using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Graybox;
using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>
	/// Something dropped that the player picks up by walking over it. The drop is put in the stash at
	/// once, so loot is kept even if the run ends badly.
	/// </summary>
	public sealed class LootPickup : MonoBehaviour
	{
		public string ItemId;
		public int Quantity;

		/// <summary>Same on every machine for the same pickup, set when playing online. Zero offline.</summary>
		public int NetId;

		/// <summary>True on a client: only shown, the host decides who picks it up.</summary>
		public bool Proxy;

		float _phase;

		/// <summary>Drops an item on the floor at <paramref name="position"/>.</summary>
		public static LootPickup Spawn(string itemId, int quantity, Vector3 position)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = "Loot_" + itemId;
			Destroy(go.GetComponent<Collider>());
			go.transform.position = new Vector3(position.x, 0.6f, position.z);
			go.transform.localScale = Vector3.one * 0.4f;
			go.GetComponent<Renderer>().sharedMaterial = Fx.Material(itemId == Loot.Gold ? new Color(1f, 0.82f, 0.2f) : itemId == Loot.HealthOrb ? new Color(0.3f, 1f, 0.4f) : new Color(0.85f, 0.85f, 0.9f));
			go.AddComponent<AssetIdTag>().Id = "loot/" + itemId;
			var pickup = go.AddComponent<LootPickup>();
			pickup.ItemId = itemId;
			pickup.Quantity = quantity;
			return pickup;
		}

		void Update()
		{
			_phase += Time.deltaTime;
			transform.rotation = Quaternion.Euler(30f, _phase * 120f, 0f);
			transform.position = new Vector3(transform.position.x, 0.6f + Mathf.Sin(_phase * 3f) * 0.12f, transform.position.z);

			// Online, the host checks every hero (DungeonNet); a client only shows the pickup.
			if (Proxy || DungeonNet.Active != null) return;

			var game = DungeonGame.Instance;
			if (game == null || game.PlayerHealth == null || !game.PlayerHealth.IsAlive) return;
			var flat = game.PlayerHealth.transform.position - transform.position;
			flat.y = 0f;
			if (flat.magnitude > 1.6f) return;

			game.Collect(ItemId, Quantity);
			Destroy(gameObject);
		}
	}
}
