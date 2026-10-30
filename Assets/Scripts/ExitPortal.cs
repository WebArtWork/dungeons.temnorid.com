using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>The way out. It opens when the boss is dead; stepping in finishes the run once the chest has been opened.</summary>
	[RequireComponent(typeof(BoxCollider))]
	public sealed class ExitPortal : MonoBehaviour
	{
		public bool IsOpen { get; private set; }

		public void Open()
		{
			IsOpen = true;
			gameObject.SetActive(true);
		}

		void OnTriggerEnter(Collider other)
		{
			if (!IsOpen || other.GetComponentInParent<PlayerFighter>() == null) return;
			DungeonGame.Instance?.TryFinish();
		}
	}
}
