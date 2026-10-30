using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>A door that stays shut until every monster of its room is dead.</summary>
	public sealed class RoomGate : MonoBehaviour
	{
		/// <summary>The room whose monsters must all be dead for this gate to open.</summary>
		public int Room;

		public bool IsOpen { get; private set; }

		public void Open()
		{
			if (IsOpen) return;
			IsOpen = true;
			gameObject.SetActive(false); // takes its collider and its NavMesh obstacle with it
		}
	}
}
