using UnityEngine;

namespace Temnorid.Dungeons
{
	/// <summary>The treasure chest. It appears where the boss falls and opens with E.</summary>
	public sealed class Chest : MonoBehaviour
	{
		public Transform Lid;

		public bool Revealed { get; private set; }
		public bool Opened { get; private set; }
		public bool CanOpen => Revealed && !Opened;

		public void Reveal(Vector3 position)
		{
			transform.position = new Vector3(position.x, 0f, position.z);
			gameObject.SetActive(true);
			Revealed = true;
		}

		public void Open()
		{
			Opened = true;
			if (Lid != null) Lid.localRotation = Quaternion.Euler(-75f, 0f, 0f);
		}

		public float DistanceFrom(Vector3 point)
		{
			var flat = point - transform.position;
			flat.y = 0f;
			return flat.magnitude;
		}
	}
}
