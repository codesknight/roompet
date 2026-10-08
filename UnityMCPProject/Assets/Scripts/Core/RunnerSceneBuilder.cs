using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// Wires the scene up. Called once from the editor menu (so the hierarchy is real and
    /// inspectable) and defensively at runtime, which makes the game playable even in a
    /// scene that was never built.
    /// </summary>
    public static class RunnerSceneBuilder
    {
        public const string RootName = "GameRoot";
        public const string PlayerName = "Cube";

        public static GameManager Build(bool editorMode)
        {
            var root = FindOrCreate(RootName);

            var manager = Ensure<GameManager>(root);
            var track = Ensure<TrackManager>(root);
            var powerUps = Ensure<PowerUpSystem>(root);
            var hud = Ensure<HudController>(root);
            _ = hud;

            var player = BuildPlayer(editorMode);
            var camera = BuildCamera(editorMode);

            manager.Track = track;
            manager.PowerUps = powerUps;
            manager.Player = player.GetComponent<PlayerController>();
            manager.CameraRig = camera;

            return manager;
        }

        // ------------------------------------------------------------------- player

        private static GameObject BuildPlayer(bool editorMode)
        {
            var player = GameObject.Find(PlayerName);
            if (player == null)
            {
                player = new GameObject(PlayerName);
                var mesh = player.AddComponent<MeshFilter>();
                mesh.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                player.AddComponent<MeshRenderer>();
            }

            player.transform.position = new Vector3(GameConfig.LaneX(GameConfig.LaneCount / 2), 0.5f, 0f);
            player.transform.rotation = Quaternion.identity;
            player.transform.localScale = Vector3.one;

            var collider = player.GetComponent<BoxCollider>();
            if (collider == null) collider = player.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.size = new Vector3(GameConfig.PlayerSize, 1f, GameConfig.PlayerSize);
            collider.center = Vector3.zero;

            var body = player.GetComponent<Rigidbody>();
            if (body == null) body = player.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            // See PlayerController.Awake: interpolation would fight the transform-driven
            // movement and desync the distance counter.
            body.interpolation = RigidbodyInterpolation.None;

            Ensure<PlayerController>(player);
            MoveVisualToChild(player, editorMode);
            return player;
        }

        /// <summary>
        /// The mesh lives on a child called "Visual" so the root transform can stay
        /// axis-aligned while the visual rolls, tilts and squashes. The visual carries an
        /// <see cref="AnimalAvatar"/>, which builds the character out of primitives.
        /// </summary>
        private static void MoveVisualToChild(GameObject player, bool editorMode)
        {
            var visual = player.transform.Find("Visual");

            if (visual == null)
            {
                var rootFilter = player.GetComponent<MeshFilter>();
                var rootRenderer = player.GetComponent<MeshRenderer>();

                var created = new GameObject("Visual");
                created.transform.SetParent(player.transform, false);
                visual = created.transform;

                SafeDestroy(rootRenderer, editorMode);
                SafeDestroy(rootFilter, editorMode);
            }

            // Any stray mesh left over from the cube build would z-fight with the avatar.
            var strayRenderer = visual.GetComponent<MeshRenderer>();
            if (strayRenderer != null)
            {
                visual.localScale = Vector3.one;
                SafeDestroy(strayRenderer, editorMode);
            }
            SafeDestroy(visual.GetComponent<MeshFilter>(), editorMode);

            var avatar = visual.GetComponent<AnimalAvatar>();
            if (avatar == null) avatar = visual.gameObject.AddComponent<AnimalAvatar>();
            avatar.Build(editorMode);
        }

        // ------------------------------------------------------------------- camera

        private static FollowCamera BuildCamera(bool editorMode)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                _ = editorMode;
            }

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 400f;
            camera.fieldOfView = 62f;

            var rig = Ensure<FollowCamera>(camera.gameObject);
            var player = GameObject.Find(PlayerName);
            if (player != null) rig.Target = player.transform;
            return rig;
        }

        // -------------------------------------------------------------------- utils

        private static GameObject FindOrCreate(string name)
        {
            var found = GameObject.Find(name);
            return found != null ? found : new GameObject(name);
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void SafeDestroy(Object target, bool editorMode)
        {
            if (target == null) return;
            if (editorMode) Object.DestroyImmediate(target);
            else Object.Destroy(target);
        }
    }
}
