using ITKamianets.Engine.UI;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using System.Text;
using ITKamianets.Engine.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Temnorid.Dungeons
{
	/// <summary>
	/// The screen: health, the shockwave, monsters left, the boss's health, loot, prompts and messages, and the
	/// pause, victory and defeat menus. Plain uGUI built in code; it only shows what <see cref="DungeonGame"/> says.
	/// </summary>
	public sealed class DungeonHud : MonoBehaviour
	{
		public DungeonGame Game;
		public PlayerFighter Fighter;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.72f);
		static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

		/// <summary>Tests set this to build the touch controls on a desktop.</summary>
		public static bool ForceTouch;

		public bool HasTouchControls { get; private set; }

		ShellChrome _chrome;
		GameObject _heroPanel;
		Text _heroText;
		UiBuilder _ui;
		Text _health, _ability, _status, _loot, _prompt, _toast, _bossName;
		Image _healthFill, _abilityFill, _bossFill;
		GameObject _bossBar, _pause, _victory, _defeat;
		Text _victoryTitle, _victoryBody;
		readonly StringBuilder _text = new StringBuilder();

		void Start()
		{
			_ui = new UiBuilder();
			EnsureEventSystem();

			var canvasObject = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			canvasObject.transform.SetParent(transform, false);
			canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
			var scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;

			// The shell's bars (top, and the places at the bottom on a phone); the game's own screen lives between them.
			_chrome = ShellChrome.Create(transform);
			_chrome.PlaceSelected += OnPlace;
			var hudObject = new GameObject("Hud", typeof(RectTransform));
			hudObject.transform.SetParent(canvasObject.transform, false);
			var hudRect = (RectTransform)hudObject.transform;
			hudRect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			hudRect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			hudRect.offsetMin = Vector2.zero;
			hudRect.offsetMax = Vector2.zero;
			var root = hudObject.transform;
			var touch = TouchButton.IsNeeded || ForceTouch;
			if (touch) BuildLookArea(root);

			// Top left: health and the shockwave.
			var left = Box("PlayerPanel", root, new Vector2(0.015f, 0.84f), new Vector2(0.27f, 0.985f), Panel);
			_health = Label(left.transform, "Health", "", 30, TextAnchor.UpperLeft, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.97f), Color.white);
			_healthFill = Bar(left.transform, "HealthBar", new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.56f), new Color(0.85f, 0.15f, 0.12f));
			_ability = Label(left.transform, "Ability", "", 26, TextAnchor.UpperLeft, new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.4f), Color.white);
			_abilityFill = Bar(left.transform, "AbilityBar", new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.14f), new Color(0.3f, 0.8f, 1f));

			// Top centre: progress, then the boss's health when the boss is near.
			_status = Label(root, "Status", "", 34, TextAnchor.UpperCenter, new Vector2(0.3f, 0.93f), new Vector2(0.7f, 0.99f), Color.white);
			_bossBar = new GameObject("BossBar", typeof(RectTransform));
			_bossBar.transform.SetParent(root, false);
			var bossRect = (RectTransform)_bossBar.transform;
			bossRect.anchorMin = new Vector2(0.3f, 0.85f);
			bossRect.anchorMax = new Vector2(0.7f, 0.93f);
			bossRect.offsetMin = Vector2.zero;
			bossRect.offsetMax = Vector2.zero;
			_bossName = Label(_bossBar.transform, "Name", "THE WARDEN", 28, TextAnchor.MiddleCenter, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Color(1f, 0.6f, 0.5f));
			_bossFill = Bar(_bossBar.transform, "BossFill", new Vector2(0f, 0.05f), new Vector2(1f, 0.45f), new Color(0.75f, 0.1f, 0.1f));

			// Top right: loot of this run.
			var right = Box("LootPanel", root, new Vector2(0.75f, 0.7f), new Vector2(0.985f, 0.985f), Panel);
			_loot = Label(right.transform, "Text", "", 28, TextAnchor.UpperLeft, new Vector2(0.06f, 0.04f), new Vector2(0.96f, 0.96f), Color.white);

			_prompt = Label(root, "Prompt", "", 38, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.16f), new Vector2(0.8f, 0.24f), Color.white);
			_toast = Label(root, "Toast", "", 42, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.7f), new Vector2(0.85f, 0.78f), Gold);
			if (!touch) Label(root, "Hint", "WASD move  ·  Shift run  ·  Space jump  ·  left mouse sword  ·  right mouse shockwave  ·  E chest  ·  Esc pause", 24, TextAnchor.LowerLeft, new Vector2(0.015f, 0.01f), new Vector2(0.95f, 0.06f), new Color(1f, 1f, 1f, 0.7f));
			else BuildTouch(root);

			_pause = Box("PauseMenu", root, new Vector2(0.35f, 0.32f), new Vector2(0.65f, 0.68f), new Color(0.06f, 0.05f, 0.09f, 0.92f));
			Label(_pause.transform, "Title", "PAUSED", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.68f), new Vector2(1f, 0.96f), Gold);
			Button(_pause.transform, "Resume", new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.62f), () => Game.SetPaused(false));
			Button(_pause.transform, "Save, main menu", new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.34f), () => Game.QuitToMenu());
			_pause.SetActive(false);

			_victory = Box("Victory", root, new Vector2(0.28f, 0.14f), new Vector2(0.72f, 0.86f), new Color(0.06f, 0.05f, 0.09f, 0.94f));
			_victoryTitle = Label(_victory.transform, "Title", "DUNGEON CLEARED", 60, TextAnchor.MiddleCenter, new Vector2(0f, 0.82f), new Vector2(1f, 0.97f), Gold);
			_victoryBody = Label(_victory.transform, "Body", "", 32, TextAnchor.UpperCenter, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.8f), Color.white);
			var again = Game.Net == null;
			if (again) Button(_victory.transform, "Play again", new Vector2(0.2f, 0.19f), new Vector2(0.8f, 0.31f), () => Game.Retry());
			Button(_victory.transform, "Main menu", new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.16f), () => Game.QuitToMenu());
			_victory.SetActive(false);

			_defeat = Box("Defeat", root, new Vector2(0.32f, 0.3f), new Vector2(0.68f, 0.7f), new Color(0.1f, 0.03f, 0.03f, 0.94f));
			Label(_defeat.transform, "Title", "YOU DIED", 64, TextAnchor.MiddleCenter, new Vector2(0f, 0.62f), new Vector2(1f, 0.95f), new Color(1f, 0.4f, 0.35f));
			Label(_defeat.transform, "Note", "What you looted is kept.", 30, TextAnchor.MiddleCenter, new Vector2(0f, 0.5f), new Vector2(1f, 0.62f), Color.white);
			if (again) Button(_defeat.transform, "Retry", new Vector2(0.2f, 0.26f), new Vector2(0.8f, 0.46f), () => Game.Retry());
			Button(_defeat.transform, "Main menu", new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.22f), () => Game.QuitToMenu());
			_defeat.SetActive(false);

			_heroPanel = Box("HeroPanel", root, new Vector2(0.2f, 0.06f), new Vector2(0.8f, 0.94f), new Color(0.06f, 0.05f, 0.09f, 0.94f));
			Label(_heroPanel.transform, "Title", "HERO", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.86f), new Vector2(1f, 0.98f), Gold);
			_heroText = Label(_heroPanel.transform, "Text", "", 30, TextAnchor.UpperLeft, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.85f), Color.white);
			Button(_heroPanel.transform, "Back to the dungeon", new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.16f), () => _chrome.Select("dungeon"));
			_heroPanel.SetActive(false);
		}

		void OnPlace(string id)
		{
			if (Game == null) return;
			_heroPanel.SetActive(id == "hero");
			// Any place but the dungeon holds the game (offline); the profile panel is the shell's own.
			Game.SetUiHold(id != "dungeon");
		}

		// ---- touch: a stick on the left, buttons on the right, dragging anywhere else turns the view ----

		void BuildLookArea(Transform parent)
		{
			var area = UiBuilder.Box("LookArea", parent, Vector2.zero, Vector2.one, Color.clear);
			area.AddComponent<TouchLookArea>();
		}

		void BuildTouch(Transform parent)
		{
			HasTouchControls = true;
			var holder = UiBuilder.Box("Touch", parent, Vector2.zero, Vector2.one, Color.clear);
			holder.GetComponent<Image>().raycastTarget = false;
			var t = holder.transform;

			var stickBase = UiBuilder.Box("StickBase", t, new Vector2(0.03f, 0.07f), new Vector2(0.2f, 0.5f), new Color(1f, 1f, 1f, 0.1f));
			stickBase.GetComponent<Image>().raycastTarget = false;
			var knob = UiBuilder.Box("StickKnob", stickBase.transform, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Color(1f, 1f, 1f, 0.3f));
			knob.SetActive(false);
			var stick = knob.AddComponent<OnScreenStick>();
			stick.controlPath = EngineTouchControls.Path(EngineTouchControls.Move);
			stick.movementRange = 120f;
			knob.SetActive(true);

			TouchButtonFor(t, "Attack", EngineTouchControls.Primary, new Vector2(0.8f, 0.06f), new Vector2(0.97f, 0.34f), 36);
			TouchButtonFor(t, "Wave", EngineTouchControls.Secondary, new Vector2(0.64f, 0.1f), new Vector2(0.78f, 0.32f), 32);
			TouchButtonFor(t, "Jump", EngineTouchControls.Jump, new Vector2(0.83f, 0.38f), new Vector2(0.96f, 0.58f), 32);
			TouchButtonFor(t, "Use", EngineTouchControls.Interact, new Vector2(0.68f, 0.36f), new Vector2(0.81f, 0.54f), 32);
			TouchButtonFor(t, "Run", EngineTouchControls.Sprint, new Vector2(0.22f, 0.07f), new Vector2(0.33f, 0.22f), 30);
		}

		void TouchButtonFor(Transform parent, string label, string control, Vector2 min, Vector2 max, int size)
		{
			var box = UiBuilder.Box("Touch " + label, parent, min, max, new Color(1f, 1f, 1f, 0.2f));
			Label(box.transform, "Text", label, size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.95f));
			box.SetActive(false);
			var button = box.AddComponent<OnScreenButton>();
			button.controlPath = EngineTouchControls.Path(control);
			box.SetActive(true);
		}

		void Update()
		{
			if (Game == null || _health == null) return;

			var health = Game.PlayerHealth;
			SetText(_health, $"HEALTH  {Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.MaxHealth)}");
			SetFill(_healthFill, health.Fraction);

			var ready = Fighter != null ? Fighter.AbilityReady : 1f;
			SetText(_ability, ready >= 1f ? (HasTouchControls ? "SHOCKWAVE  ready (Wave)" : "SHOCKWAVE  ready (right mouse)") : "SHOCKWAVE  recharging");
			SetFill(_abilityFill, ready);

			var minutes = (int)(Game.RunTime / 60f);
			SetText(_status, $"Monsters left {Game.MonstersLeft}     {minutes}:{Game.RunTime - minutes * 60f:00.0}");
			if (_chrome != null) _chrome.SetStatus($"Slain {Game.Kills}");

			var boss = Game.Boss;
			var bossNear = boss != null && boss.Health.IsAlive && boss.gameObject.activeInHierarchy
				&& Vector3.Distance(boss.transform.position, health.transform.position) < 28f;
			_bossBar.SetActive(bossNear);
			if (bossNear) SetFill(_bossFill, boss.Health.Fraction);

			SetText(_loot, LootText());
			SetText(_prompt, Game.LocalDown && Game.Phase == DungeonPhase.Playing ? "You have fallen. Your allies fight on." : Fighter != null ? Fighter.Prompt : "");
			if (_heroPanel.activeSelf) SetText(_heroText, HeroText());
			SetText(_toast, Time.unscaledTime < Game.ToastUntil ? Game.ToastText : "");

			_pause.SetActive(Game.Paused);
			var won = Game.Phase == DungeonPhase.Victory;
			var lost = Game.Phase == DungeonPhase.Defeat;
			if (won != _victory.activeSelf)
			{
				_victory.SetActive(won);
				if (won)
				{
					SetText(_victoryBody, VictoryText());
					Select(_victory);
				}
			}
			if (lost != _defeat.activeSelf)
			{
				_defeat.SetActive(lost);
				if (lost) Select(_defeat);
			}
			if (Game.Paused && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null) Select(_pause);
		}

		string HeroText()
		{
			var session = Game.Session;
			var health = Game.PlayerHealth;
			_text.Clear();
			_text.Append("Health  ").Append(Mathf.CeilToInt(health.Current)).Append(" / ").Append(Mathf.CeilToInt(health.MaxHealth)).Append('\n');
			_text.Append("Monsters slain  ").Append(Game.Kills).Append("     left  ").Append(Game.MonstersLeft).Append('\n');
			_text.Append("Dungeons cleared  ").Append(session.Completions);
			if (session.BestSeconds > 0f) _text.Append("     best  ").Append(Format(session.BestSeconds));
			_text.Append("\n\nLoot this run\n");
			if (session.RunLoot.Count == 0) _text.Append("nothing yet\n");
			foreach (var pair in session.RunLoot) _text.Append(Loot.Name(pair.Key)).Append("  ").Append(pair.Value).Append('\n');
			if (Game.Net != null)
			{
				_text.Append("\nParty\n");
				var net = NetSession.Instance;
				foreach (var player in net.Players)
				{
					_text.Append(player.Name).Append("  ");
					if (player.Id == net.LocalId) _text.Append(Game.LocalDown ? "fallen" : Mathf.CeilToInt(health.Current) + " hp");
					else if (Game.Net.Heroes.TryGetValue(player.Id, out var other)) _text.Append(other.Dead ? "fallen" : Mathf.CeilToInt(other.Health) + " hp");
					_text.Append('\n');
				}
			}
			_text.Append(TouchButton.IsNeeded || ForceTouch
				? "\nLeft stick moves, drag to look. Attack, Wave (shockwave), Jump, Use (the chest) and Run are on the right."
				: "\nWASD move, Shift run, Space jump, left mouse sword, right mouse shockwave, E chest, V view, Esc pause.");
			return _text.ToString();
		}

		string LootText()
		{
			_text.Clear();
			_text.Append("LOOT THIS RUN\n");
			if (Game.Session.RunLoot.Count == 0) _text.Append("nothing yet\n");
			foreach (var pair in Game.Session.RunLoot) _text.Append(Loot.Name(pair.Key)).Append("  ").Append(pair.Value).Append('\n');
			return _text.ToString();
		}

		string VictoryText()
		{
			var session = Game.Session;
			_text.Clear();
			_text.Append("Time  ").Append(Format(Game.RunTime));
			if (Game.NewBestTime) _text.Append("   (new best!)");
			else _text.Append("   best  ").Append(Format(session.BestSeconds));
			_text.Append("\n\nLooted this run\n");
			foreach (var pair in session.RunLoot) _text.Append(Loot.Name(pair.Key)).Append("  ").Append(pair.Value).Append('\n');
			_text.Append("\nDungeons cleared  ").Append(session.Completions);
			return _text.ToString();
		}

		static string Format(float seconds)
		{
			var minutes = (int)(seconds / 60f);
			return $"{minutes}:{seconds - minutes * 60f:00.0}";
		}

		static void SetText(Text label, string value)
		{
			if (label.text != value) label.text = value;
		}

		static void SetFill(Image fill, float fraction)
		{
			fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
		}

		static void Select(GameObject screen)
		{
			if (EventSystem.current == null) return;
			var button = screen.GetComponentInChildren<UnityEngine.UI.Button>();
			EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
		}

		// ---- building blocks (shared: 3d-unity-ui) ----

		void EnsureEventSystem() => UiBuilder.EnsureEventSystem(transform);

		static GameObject Box(string name, Transform parent, Vector2 min, Vector2 max, Color colour) => UiBuilder.Box(name, parent, min, max, colour);

		static Image Bar(Transform parent, string name, Vector2 min, Vector2 max, Color colour) => UiBuilder.Bar(name, parent, min, max, colour);

		Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color colour) => _ui.Label(name, parent, text, size, anchor, min, max, colour);

		void Button(Transform parent, string text, Vector2 min, Vector2 max, System.Action onClick) => _ui.Button(text, parent, min, max, onClick);
	}
}
