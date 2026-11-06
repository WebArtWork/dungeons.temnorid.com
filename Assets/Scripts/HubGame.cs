using System.Collections;
using ITKamianets.Engine.Account;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using Temnorid.Village;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Temnorid.Dungeons
{
	/// <summary>
	/// Where the game opens: the lord's arena in the village. On its north wall hangs the dungeon board, a plaque for each of the six dungeons; walk up
	/// to one and press E to send the hero there. The hero is read from Lords first: if Lords has it away in a dungeon, the player is taken straight to that
	/// dungeon instead of the arena. Sending the hero from here writes the same trip into Lords.
	/// </summary>
	public sealed class HubGame : MonoBehaviour
	{
		public const string DungeonScene = "Dungeon";
		public const string HubScene = "Main";

		[Tooltip("A lit material of the scene: the arena is made of copies of it in other colours.")]
		public Material Lit;
		[Tooltip("The text material the board's writing uses.")]
		public Material TextMaterial;
		public Transform Player;

		const float PlaqueSpacing = 4.4f;
		const float PlaqueReach = 2.2f;
		const float WallReach = 7f;
		const string Log = "Dungeons";

		ArenaPlaces _places;
		UiBuilder _ui;
		ShellChrome _chrome;
		Text _prompt, _footer;
		float _boardZ;
		bool _loaded, _busy;
		float _refreshAt;
		string _note = "";
		float _noteUntil;

		void Awake()
		{
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			_places = VillageArena.Build(transform, Lit, false);
			_boardZ = VillageArena.Depth * 0.5f - 0.7f;
			BuildBoard();
			if (Player != null)
			{
				var controller = Player.GetComponent<CharacterController>();
				if (controller != null) controller.enabled = false;
				Player.SetPositionAndRotation(_places.PlayerSpawn, _places.PlayerRotation);
				if (controller != null) controller.enabled = true;
			}
		}

		void Start()
		{
			_ui = new UiBuilder();
			UiBuilder.EnsureEventSystem(transform);

			var canvasObject = new GameObject("HubCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			canvasObject.transform.SetParent(transform, false);
			canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
			var scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;

			_chrome = ShellChrome.Create(transform);
			_chrome.SetTitle("Arena");

			var hud = new GameObject("Hud", typeof(RectTransform));
			hud.transform.SetParent(canvasObject.transform, false);
			var rect = (RectTransform)hud.transform;
			rect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			rect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			// What the plaque in front of the player says, and the key. No window: the board is the one on the wall.
			_prompt = _ui.Label("Prompt", hud.transform, "", 36, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.27f), Color.white, wrap: true);
			var footer = UiBuilder.Box("Footer", hud.transform, new Vector2(0.28f, 0.015f), new Vector2(0.72f, 0.075f), new Color(0.06f, 0.05f, 0.09f, 0.78f));
			footer.GetComponent<Image>().raycastTarget = false;
			_footer = _ui.Label("Hero", footer.transform, "Reading your hero...", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);

			StartCoroutine(Load());
		}

		IEnumerator Load()
		{
			// The shell restores a saved sign-in in the background; give it a few seconds, never longer.
			var end = Time.unscaledTime + 5f;
			while (GameAccount.Busy && Time.unscaledTime < end) yield return null;

			// A trip that has just ended in the dungeon may still be on its way to Lords.
			while (HeroLink.Pending != null && !HeroLink.Pending.IsCompleted && Time.unscaledTime < end + 10f) yield return null;
			HeroLink.Reset();
			var task = HeroLink.Current.LoadAsync();
			while (!task.IsCompleted) yield return null;
			_loaded = true;

			// If Lords has the hero away in a dungeon, that is where the player is.
			var link = HeroLink.Current;
			if (link.StatusNow == HeroStatus.Away && link.Hero.Expedition != null && DungeonCatalog.ById(link.Hero.Expedition.Dungeon) != null)
			{
				DungeonSelection.Id = link.Hero.Expedition.Dungeon;
				EngineLog.Info(Log, "Lords has the hero in " + DungeonSelection.Id + ": going there.");
				Time.timeScale = 1f;
				SceneManager.LoadScene(DungeonScene);
				yield break;
			}
			RefreshFooter();
		}

		// ---- the board on the wall ----

		float PlaqueX(int index) => (index - (DungeonCatalog.All.Length - 1) * 0.5f) * PlaqueSpacing;

		void BuildBoard()
		{
			var wood = new Material(Lit) { color = new Color(0.36f, 0.24f, 0.14f) };
			var dark = new Material(Lit) { color = new Color(0.16f, 0.12f, 0.09f) };
			var root = new GameObject("DungeonBoard").transform;
			root.SetParent(transform, false);
			root.position = new Vector3(0f, 0f, _boardZ);

			var count = DungeonCatalog.All.Length;
			var width = (count - 1) * PlaqueSpacing + 4.2f;
			// The frame on the wall, with a beam above it that says what it is.
			Piece(PrimitiveType.Cube, "Backing", root, new Vector3(0f, 2.5f, 0.1f), new Vector3(width, 3.6f, 0.2f), wood);
			Piece(PrimitiveType.Cube, "Beam", root, new Vector3(0f, 4.55f, 0.05f), new Vector3(width, 0.7f, 0.3f), wood);
			Text(root, "Title", "DUNGEONS", new Vector3(0f, 4.55f, -0.15f), 0.07f, new Color(1f, 0.82f, 0.3f));

			for (var i = 0; i < count; i++)
			{
				var dungeon = DungeonCatalog.All[i];
				var x = PlaqueX(i);
				var rim = dungeon.Danger == 1 ? new Color(0.35f, 0.7f, 0.35f) : dungeon.Danger == 2 ? new Color(0.9f, 0.6f, 0.2f) : new Color(0.8f, 0.2f, 0.18f);
				Piece(PrimitiveType.Cube, "Rim" + i, root, new Vector3(x, 2.5f, -0.02f), new Vector3(3.6f, 3.0f, 0.12f), new Material(Lit) { color = rim });
				Piece(PrimitiveType.Cube, "Plaque" + i, root, new Vector3(x, 2.5f, -0.1f), new Vector3(3.3f, 2.7f, 0.12f), dark);
				Text(root, "Name" + i, dungeon.Name.Replace(' ', '\n'), new Vector3(x, 2.95f, -0.2f), 0.05f, new Color(0.96f, 0.9f, 0.7f));
				Text(root, "Danger" + i, new string('*', dungeon.Danger), new Vector3(x, 1.75f, -0.2f), 0.08f, rim);
			}
		}

		void Text(Transform parent, string name, string text, Vector3 localPosition, float size, Color colour)
		{
			var go = new GameObject(name);
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localPosition;
			var mesh = go.AddComponent<TextMesh>();
			mesh.text = text;
			mesh.characterSize = size;
			mesh.fontSize = 64;
			mesh.anchor = TextAnchor.MiddleCenter;
			mesh.alignment = TextAlignment.Center;
			mesh.color = colour;
			mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			if (TextMaterial != null) go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial;
		}

		static Transform Piece(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			var c = piece.GetComponent<Collider>();
			if (c != null) Destroy(c);
			piece.transform.SetParent(parent, false);
			piece.transform.localPosition = localPosition;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
			return piece.transform;
		}

		/// <summary>The plaque the player stands in front of, or -1.</summary>
		int PlaqueInFront()
		{
			if (Player == null) return -1;
			var position = Player.position;
			if (_boardZ - position.z > WallReach) return -1;
			var best = -1;
			var bestGap = PlaqueReach;
			for (var i = 0; i < DungeonCatalog.All.Length; i++)
			{
				var gap = Mathf.Abs(position.x - PlaqueX(i));
				if (gap < bestGap) { bestGap = gap; best = i; }
			}
			return best;
		}

		IEnumerator Go(DungeonDef dungeon)
		{
			_busy = true;
			_note = "Sending the hero...";
			var task = HeroLink.Current.SendAsync(dungeon);
			while (!task.IsCompleted) yield return null;
			_busy = false;

			var error = task.IsFaulted ? "Could not send the hero." : task.Result;
			if (error.Length > 0)
			{
				_note = error;
				_noteUntil = Time.unscaledTime + 4f;
				yield break;
			}

			_note = "Off to " + dungeon.Name + "!";
			_busy = true;
			EngineLog.Info(Log, "The hero goes to " + dungeon.Id + ".");
			Time.timeScale = 1f;
			SceneManager.LoadScene(DungeonScene);
		}

		// ---- the screen: the hero's line, and what the plaque in front says ----

		void RefreshFooter()
		{
			if (_footer == null) return;
			var link = HeroLink.Current;
			var health = Mathf.FloorToInt((float)link.HealthNow);
			var state = link.StatusNow;
			var text = $"Hero {health} / {Mathf.RoundToInt((float)HeroRules.MaxHealth)}";
			if (state == HeroStatus.Infirmary) text += "  ·  in the infirmary";
			else if (state == HeroStatus.Back) text += "  ·  back from a trip: the loot waits in Lords";
			else if (state == HeroStatus.Away) text += "  ·  away";
			_footer.text = text + "  ·  " + link.Status;
		}

		void Update()
		{
			if (_prompt == null || !_loaded) return;

			if (Time.unscaledTime >= _refreshAt)
			{
				_refreshAt = Time.unscaledTime + 1f;
				RefreshFooter();
			}

			if (_busy || Time.unscaledTime < _noteUntil)
			{
				_prompt.text = _note;
				return;
			}

			var index = PlaqueInFront();
			if (index < 0)
			{
				_prompt.text = "";
				return;
			}

			var dungeon = DungeonCatalog.All[index];
			var why = HeroLink.Current.WhyNot(dungeon);
			var details = $"danger {dungeon.Danger}/3  ·  {dungeon.Distance} km  ·  {dungeon.Minutes} min  ·  loot: {dungeon.LootText()}";
			_prompt.text = (why == CannotGo.None ? $"E  -  go to {dungeon.Name}" : dungeon.Name + ": " + HeroLink.Reason(why)) + "\n" + details;

			if (why == CannotGo.None && EngineInput.Default.Player.Interact.WasPressedThisFrame()) StartCoroutine(Go(dungeon));
		}
	}
}
