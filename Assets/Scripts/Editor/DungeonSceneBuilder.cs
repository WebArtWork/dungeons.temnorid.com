using ITKamianets.Engine.Graybox.EditorTools;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Graybox;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Shell.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Temnorid.Dungeons.EditorTools
{
	/// <summary>
	/// Builds the whole dungeon from Unity primitives: an entrance, two corridors with rooms of monsters,
	/// a boss room with a chest and an exit, plus the player, camera, lights and screens. Run it from the menu
	/// (Temnorid > Dungeons > Build Scene) or the command line
	/// (<c>-executeMethod Temnorid.Dungeons.EditorTools.DungeonSceneBuilder.Build</c>); it rebuilds
	/// <c>Assets/Scenes/Main.unity</c> from scratch each time, so do not edit that scene by hand.
	/// The NavMesh is baked when the game starts, from the floor and wall colliders.
	/// </summary>
	public static class DungeonSceneBuilder
	{
		const string ScenePath = "Assets/Scenes/Main.unity";
		public const string Version = "0.2.0";
		const string MaterialDir = "Assets/Materials";
		const float WallHeight = 4f;

		[MenuItem("Temnorid/Dungeons/Build Scene")]
		public static void Build()
		{
			ShellSceneKit.ApplyPlatformSettings("Temnorid", "Temnorid Dungeons", "com.itkamianets.dungeons", Version);
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.itkamianets.dungeons");
			PlayerSettings.runInBackground = true;
			PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.36f, 0.35f, 0.44f);

			var dungeon = BuildLevel(out var surface);
			var gates = BuildGates();
			var monsters = BuildMonsters(out var boss);
			var chest = BuildChest();
			var exit = BuildExit();
			var player = BuildPlayer();
			BuildCameraAndLights(player.GetComponent<CharacterMotor>());

			var gameObject = new GameObject("Game");
			var game = gameObject.AddComponent<DungeonGame>();
			game.PlayerHealth = player.GetComponent<Health>();
			game.Surface = surface;
			game.StartingMonsters = monsters;
			game.Boss = boss;
			game.Gates = gates;
			game.Chest = chest;
			game.Exit = exit;
			player.GetComponent<PlayerFighter>().Game = game;

			var hud = gameObject.AddComponent<DungeonHud>();
			hud.Game = game;
			hud.Fighter = player.GetComponent<PlayerFighter>();

			EditorSceneManager.SaveScene(scene, ScenePath);
			var dungeonName = dungeon.name;
			var monsterCount = monsters.Length;
			ShellSceneKit.BuildBoot();
			ShellSceneKit.BuildMenu();
			ShellSceneKit.SetBuildOrder(ScenePath);
			AssetDatabase.SaveAssets();
			Debug.Log("[Dungeons] Scene built: " + ScenePath + " (" + dungeonName + ", " + monsterCount + " monsters)");
		}

		// ---- the level ----

		static Transform BuildLevel(out NavMeshSurface surface)
		{
			var floor = Mat("Floor", new Color(0.30f, 0.29f, 0.33f));
			var wall = Mat("Wall", new Color(0.42f, 0.39f, 0.45f));

			var root = new GameObject("Dungeon");
			surface = root.AddComponent<NavMeshSurface>();
			surface.collectObjects = CollectObjects.Children;
			surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
			var t = root.transform;

			// Rooms and the corridors between them, from the entrance (south) to the boss (north).
			Room(t, "Entrance", 0f, 0f, 10f, 10f, floor, wall, openNorth: true, openSouth: false);
			Corridor(t, "Corridor1", 0f, 5f, 15f, floor, wall);
			Room(t, "RoomA", 0f, 22f, 16f, 14f, floor, wall, openNorth: true, openSouth: true);
			Corridor(t, "Corridor2", 0f, 29f, 37f, floor, wall);
			Room(t, "RoomB", 0f, 46f, 18f, 18f, floor, wall, openNorth: true, openSouth: true);
			Corridor(t, "Corridor3", 0f, 55f, 63f, floor, wall);
			Room(t, "BossRoom", 0f, 74f, 22f, 22f, floor, wall, openNorth: false, openSouth: true);
			return t;
		}

		static void Room(Transform parent, string name, float cx, float cz, float width, float depth, Material floor, Material wall, bool openNorth, bool openSouth)
		{
			var room = new GameObject(name).transform;
			room.SetParent(parent, false);
			Box(room, "Floor", new Vector3(cx, -0.25f, cz), new Vector3(width, 0.5f, depth), floor);

			const float gap = 4f; // width of a doorway
			var halfW = width * 0.5f;
			var halfD = depth * 0.5f;
			Box(room, "WallWest", new Vector3(cx - halfW, WallHeight * 0.5f, cz), new Vector3(1f, WallHeight, depth + 1f), wall);
			Box(room, "WallEast", new Vector3(cx + halfW, WallHeight * 0.5f, cz), new Vector3(1f, WallHeight, depth + 1f), wall);
			EndWall(room, "WallNorth", cx, cz + halfD, width, gap, openNorth, wall);
			EndWall(room, "WallSouth", cx, cz - halfD, width, gap, openSouth, wall);
		}

		static void EndWall(Transform room, string name, float cx, float z, float width, float gap, bool hasDoorway, Material wall)
		{
			if (!hasDoorway)
			{
				Box(room, name, new Vector3(cx, WallHeight * 0.5f, z), new Vector3(width + 1f, WallHeight, 1f), wall);
				return;
			}
			var side = (width - gap) * 0.5f;
			Box(room, name + "A", new Vector3(cx - (gap + side) * 0.5f, WallHeight * 0.5f, z), new Vector3(side + 0.5f, WallHeight, 1f), wall);
			Box(room, name + "B", new Vector3(cx + (gap + side) * 0.5f, WallHeight * 0.5f, z), new Vector3(side + 0.5f, WallHeight, 1f), wall);
		}

		static void Corridor(Transform parent, string name, float cx, float z0, float z1, Material floor, Material wall)
		{
			var corridor = new GameObject(name).transform;
			corridor.SetParent(parent, false);
			var length = z1 - z0;
			var middle = (z0 + z1) * 0.5f;
			Box(corridor, "Floor", new Vector3(cx, -0.25f, middle), new Vector3(4f, 0.5f, length), floor);
			Box(corridor, "WallWest", new Vector3(cx - 2.5f, WallHeight * 0.5f, middle), new Vector3(1f, WallHeight, length), wall);
			Box(corridor, "WallEast", new Vector3(cx + 2.5f, WallHeight * 0.5f, middle), new Vector3(1f, WallHeight, length), wall);
		}

		static RoomGate[] BuildGates()
		{
			var gateMaterial = Mat("Gate", new Color(0.45f, 0.22f, 0.16f));
			var gates = new RoomGate[2];
			var spots = new[] { new Vector3(0f, 1.75f, 30.5f), new Vector3(0f, 1.75f, 56.5f) };
			for (var i = 0; i < gates.Length; i++)
			{
				var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
				gate.name = "Gate_" + (i == 0 ? "A" : "B");
				gate.transform.position = spots[i];
				gate.transform.localScale = new Vector3(4.2f, 3.5f, 0.8f);
				gate.GetComponent<Renderer>().sharedMaterial = gateMaterial;
				var obstacle = gate.AddComponent<NavMeshObstacle>();
				obstacle.carving = true;
				gates[i] = gate.AddComponent<RoomGate>();
				gates[i].Room = i; // the gate after room A opens when room A is cleared, and so on
				gate.AddComponent<AssetIdTag>().Id = "props/gate";
			}
			return gates;
		}

		// ---- monsters ----

		static Monster[] BuildMonsters(out Monster boss)
		{
			var root = new GameObject("Monsters").transform;
			var green = Mat("Goblin", new Color(0.3f, 0.6f, 0.25f));
			var bone = Mat("Skeleton", new Color(0.85f, 0.82f, 0.7f));
			var dark = Mat("Dark", new Color(0.12f, 0.1f, 0.12f));

			var list = new System.Collections.Generic.List<Monster>
			{
				Goblin(root, "Goblin_A1", new Vector3(-4f, 0f, 21f), 0, green, dark, Loot.Gold, 5),
				Goblin(root, "Goblin_A2", new Vector3(4f, 0f, 24f), 0, green, dark, Loot.Gold, 5, healDrop: 20f),
				Goblin(root, "Skeleton_B1", new Vector3(-6f, 0f, 44f), 1, bone, dark, Loot.BoneCharm, 1),
				Goblin(root, "Skeleton_B2", new Vector3(6f, 0f, 44f), 1, bone, dark, Loot.BoneCharm, 1),
				Goblin(root, "Skeleton_B3", new Vector3(0f, 0f, 50f), 1, bone, dark, Loot.Gold, 10, healDrop: 30f),
			};
			boss = Warden(root, new Vector3(0f, 0f, 77f));
			list.Add(boss);
			return list.ToArray();
		}

		static Monster Goblin(Transform parent, string name, Vector3 position, int room, Material body, Material dark, string lootId, int lootQuantity, float healDrop = 0f)
		{
			var root = NewMonsterRoot(parent, name, position, "monsters/" + (lootId == Loot.BoneCharm ? "skeleton" : "goblin"));
			var monster = Configure(root, name.StartsWith("Skeleton") ? "Skeleton" : "Goblin", room, false, 40f, 7f, 3.4f, 0.45f, 1.8f, 1.9f, 1.3f);
			monster.LootId = lootId;
			monster.LootQuantity = lootQuantity;
			monster.HealDrop = healDrop;

			var visual = new GameObject("Visual").transform;
			visual.SetParent(root.transform, false);
			Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.85f, 0f), new Vector3(0.9f, 0.85f, 0.9f), body);
			Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.75f, 0.05f), new Vector3(0.55f, 0.55f, 0.55f), body);
			Prim(PrimitiveType.Sphere, "EyeL", visual, new Vector3(-0.13f, 1.8f, 0.3f), new Vector3(0.1f, 0.1f, 0.1f), dark);
			Prim(PrimitiveType.Sphere, "EyeR", visual, new Vector3(0.13f, 1.8f, 0.3f), new Vector3(0.1f, 0.1f, 0.1f), dark);
			Prim(PrimitiveType.Cube, "Club", visual, new Vector3(0.55f, 1.0f, 0.4f), new Vector3(0.14f, 0.14f, 0.9f), dark);
			root.SetActive(false); // woken by the game once the NavMesh exists
			return monster;
		}

		static Monster Warden(Transform parent, Vector3 position)
		{
			var red = Mat("BossRed", new Color(0.55f, 0.1f, 0.12f));
			var horn = Mat("Horn", new Color(0.9f, 0.85f, 0.7f));
			var dark = Mat("Dark", new Color(0.12f, 0.1f, 0.12f));
			var glow = Mat("Glow", new Color(1f, 0.6f, 0.15f));

			var root = NewMonsterRoot(parent, "Boss_Warden", position, "monsters/boss-warden");
			var monster = Configure(root, "The Warden", 2, true, 300f, 12f, 2.8f, 0.9f, 3.2f, 3.3f, 1.6f);
			monster.SlamDamage = 24f;
			monster.SlamRadius = 5.5f;
			monster.SlamInterval = 6f;
			monster.SlamTelegraphSeconds = 1.3f;
			root.GetComponent<HealthBar3D>().Height = 4.2f;
			root.GetComponent<HealthBar3D>().Width = 2.4f;
			root.GetComponent<AiTarget>().EyeHeight = 2f;

			var visual = new GameObject("Visual").transform;
			visual.SetParent(root.transform, false);
			Prim(PrimitiveType.Cylinder, "Body", visual, new Vector3(0f, 1.4f, 0f), new Vector3(1.9f, 1.4f, 1.9f), red);
			Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 3.0f, 0.1f), new Vector3(1.1f, 1.1f, 1.1f), red);
			Prim(PrimitiveType.Cube, "HornL", visual, new Vector3(-0.5f, 3.6f, 0.1f), new Vector3(0.18f, 0.7f, 0.18f), horn).transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
			Prim(PrimitiveType.Cube, "HornR", visual, new Vector3(0.5f, 3.6f, 0.1f), new Vector3(0.18f, 0.7f, 0.18f), horn).transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
			Prim(PrimitiveType.Sphere, "EyeL", visual, new Vector3(-0.25f, 3.1f, 0.6f), new Vector3(0.18f, 0.18f, 0.18f), glow);
			Prim(PrimitiveType.Sphere, "EyeR", visual, new Vector3(0.25f, 3.1f, 0.6f), new Vector3(0.18f, 0.18f, 0.18f), glow);
			Prim(PrimitiveType.Cube, "ArmL", visual, new Vector3(-1.3f, 1.9f, 0.3f), new Vector3(0.55f, 1.6f, 0.55f), red);
			Prim(PrimitiveType.Cube, "ArmR", visual, new Vector3(1.3f, 1.9f, 0.3f), new Vector3(0.55f, 1.6f, 0.55f), red);
			Prim(PrimitiveType.Cube, "Hammer", visual, new Vector3(1.4f, 1.2f, 1.0f), new Vector3(0.7f, 0.7f, 1.6f), dark);
			root.SetActive(false);
			return monster;
		}

		static GameObject NewMonsterRoot(Transform parent, string name, Vector3 position, string assetId)
		{
			var root = new GameObject(name);
			root.transform.SetParent(parent, false);
			root.transform.position = position;
			root.AddComponent<AssetIdTag>().Id = assetId;
			return root;
		}

		static Monster Configure(GameObject root, string displayName, int room, bool isBoss, float health, float meleeDamage, float speed, float radius, float height, float attackRange, float attackCooldown)
		{
			var agent = root.AddComponent<NavMeshAgent>();
			agent.speed = speed;
			agent.acceleration = 14f;
			agent.angularSpeed = 360f;
			agent.radius = radius;
			agent.height = height;
			agent.stoppingDistance = attackRange * 0.6f;
			agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

			var capsule = root.AddComponent<CapsuleCollider>();
			capsule.radius = radius;
			capsule.height = height;
			capsule.center = new Vector3(0f, height * 0.5f, 0f);

			var life = root.AddComponent<Health>();
			life.MaxHealth = health;
			life.Team = 2;
			life.Restore();

			var marker = root.AddComponent<AiTarget>();
			marker.Team = 2;

			var ai = root.AddComponent<AiAgent>();
			ai.Team = 2;
			ai.SenseRadius = isBoss ? 22f : 13f;
			ai.LoseRadius = isBoss ? 40f : 24f;
			ai.AttackRange = attackRange;
			ai.AttackCooldown = attackCooldown;

			var monster = root.AddComponent<Monster>();
			monster.DisplayName = displayName;
			monster.Room = room;
			monster.IsBoss = isBoss;
			monster.MeleeDamage = meleeDamage;

			root.AddComponent<HealthBar3D>().Height = height + 0.5f;
			return monster;
		}

		// ---- the chest, the exit, the player ----

		static Chest BuildChest()
		{
			var wood = Mat("ChestWood", new Color(0.45f, 0.28f, 0.14f));
			var gold = Mat("ChestGold", new Color(1f, 0.8f, 0.2f));

			var root = new GameObject("Chest");
			root.AddComponent<AssetIdTag>().Id = "props/chest";
			var chest = root.AddComponent<Chest>();
			Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.8f, 0.9f), wood);
			Prim(PrimitiveType.Cube, "Band", root.transform, new Vector3(0f, 0.4f, -0.46f), new Vector3(0.2f, 0.6f, 0.05f), gold);

			var lid = new GameObject("Lid").transform; // hinged at the back edge
			lid.SetParent(root.transform, false);
			lid.localPosition = new Vector3(0f, 0.8f, 0.45f);
			Prim(PrimitiveType.Cube, "LidTop", lid, new Vector3(0f, 0.12f, -0.45f), new Vector3(1.4f, 0.25f, 0.9f), wood);
			chest.Lid = lid;

			root.SetActive(false);
			return chest;
		}

		static ExitPortal BuildExit()
		{
			var glow = Mat("Portal", new Color(0.3f, 0.85f, 1f));
			var stone = Mat("Wall", new Color(0.42f, 0.39f, 0.45f));

			var root = new GameObject("Exit");
			root.transform.position = new Vector3(0f, 0f, 82.5f);
			root.AddComponent<AssetIdTag>().Id = "props/exit-portal";
			var box = root.AddComponent<BoxCollider>();
			box.isTrigger = true;
			box.center = new Vector3(0f, 1.5f, 0f);
			box.size = new Vector3(3f, 3f, 1.6f);
			var exit = root.AddComponent<ExitPortal>();

			Prim(PrimitiveType.Cube, "PillarL", root.transform, new Vector3(-1.7f, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.6f), stone);
			Prim(PrimitiveType.Cube, "PillarR", root.transform, new Vector3(1.7f, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.6f), stone);
			Prim(PrimitiveType.Cube, "Lintel", root.transform, new Vector3(0f, 3.3f, 0f), new Vector3(4.2f, 0.5f, 0.6f), stone);
			Prim(PrimitiveType.Cube, "Glow", root.transform, new Vector3(0f, 1.5f, 0f), new Vector3(2.9f, 3f, 0.08f), glow);

			root.SetActive(false);
			return exit;
		}

		static GameObject BuildPlayer()
		{
			var blue = Mat("HeroBlue", new Color(0.2f, 0.45f, 0.9f));
			var skin = Mat("Skin", new Color(0.88f, 0.7f, 0.55f));
			var steel = Mat("Steel", new Color(0.75f, 0.78f, 0.85f));
			var leather = Mat("Leather", new Color(0.4f, 0.26f, 0.14f));

			var player = new GameObject("Player");
			player.transform.position = new Vector3(0f, 0.1f, -2.5f);
			player.AddComponent<CharacterController>();
			player.AddComponent<CharacterMotor>();
			player.AddComponent<CharacterViewControls>();

			var life = player.AddComponent<Health>();
			life.MaxHealth = 180f;
			life.Team = 1;
			life.Restore();
			var marker = player.AddComponent<AiTarget>();
			marker.Team = 1;
			player.AddComponent<PlayerFighter>();
			player.AddComponent<AssetIdTag>().Id = "characters/hero";

			var body = new GameObject("Body").transform;
			body.SetParent(player.transform, false);
			Prim(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), blue);
			Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.62f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), skin);
			Prim(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.8f, 0f), new Vector3(0.74f, 0.1f, 0.74f), leather);
			Prim(PrimitiveType.Cube, "Blade", body, new Vector3(0.5f, 1.0f, 0.45f), new Vector3(0.08f, 0.08f, 1.1f), steel);
			Prim(PrimitiveType.Cube, "Guard", body, new Vector3(0.5f, 1.0f, -0.05f), new Vector3(0.25f, 0.08f, 0.1f), leather);
			return player;
		}

		static void BuildCameraAndLights(CharacterMotor target)
		{
			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			var camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.04f, 0.04f, 0.07f);
			camera.fieldOfView = 68f;
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = 200f;
			cameraObject.AddComponent<AudioListener>();
			var view = cameraObject.AddComponent<CharacterViewCamera>();

			var serialized = new SerializedObject(view);
			serialized.FindProperty("target").objectReferenceValue = target;
			serialized.FindProperty("settings.StartView").enumValueIndex = (int)CameraView.ThirdPerson;
			serialized.FindProperty("settings.Distance").floatValue = 4.8f; // far enough back to see the fight, not so far that walls get in the way
			serialized.ApplyModifiedPropertiesWithoutUndo();

			var sun = new GameObject("Dim Light");
			sun.transform.rotation = Quaternion.Euler(60f, -20f, 0f);
			var directional = sun.AddComponent<Light>();
			directional.type = LightType.Directional;
			directional.intensity = 0.35f;
			directional.color = new Color(0.7f, 0.75f, 1f);
			directional.shadows = LightShadows.None;

			// Torches: warm lights in every room and corridor.
			var torches = new[]
			{
				new Vector3(0f, 3.4f, 0f), new Vector3(0f, 3.4f, 10f), new Vector3(-5f, 3.4f, 22f), new Vector3(5f, 3.4f, 22f),
				new Vector3(0f, 3.4f, 33f), new Vector3(-6f, 3.4f, 46f), new Vector3(6f, 3.4f, 46f), new Vector3(0f, 3.4f, 59f),
				new Vector3(-7f, 3.4f, 74f), new Vector3(7f, 3.4f, 74f), new Vector3(0f, 3.4f, 82f),
			};
			var group = new GameObject("Torches").transform;
			foreach (var position in torches)
			{
				var torch = new GameObject("Torch");
				torch.transform.SetParent(group, false);
				torch.transform.position = position;
				var light = torch.AddComponent<Light>();
				light.type = LightType.Point;
				light.range = 15f;
				light.intensity = 2.4f;
				light.color = new Color(1f, 0.72f, 0.45f);
				light.shadows = LightShadows.None;
			}
		}

		// ---- helpers (shared: 3d-unity-graybox) ----

		static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material) => GrayboxBuilder.Box(parent, name, position, scale, material);

		static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false) => GrayboxBuilder.Prim(type, name, parent, position, scale, material, keepCollider);

		static Material Mat(string name, Color colour) => GrayboxBuilder.Mat(name, colour, MaterialDir);
	}
}
