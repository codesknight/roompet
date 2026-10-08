using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Builds the room the pet lives in and the objects the player can interact with.
    /// Everything is primitives plus the CC0 Kenney props already imported for the runner,
    /// so the whole environment is reproducible from code.
    /// </summary>
    public class PetRoom : MonoBehaviour
    {
        [Header("Room")]
        public float Size = 14f;
        public float WallHeight = 4f;

        [Header("Palette")]
        public Color FloorColor = new Color(0.62f, 0.47f, 0.36f);
        public Color WallColor = new Color(0.86f, 0.82f, 0.76f);
        public Color AccentWallColor = new Color(0.55f, 0.70f, 0.78f);
        public Color RugColor = new Color(0.78f, 0.35f, 0.38f);

        public readonly List<Interactable> Interactables = new List<Interactable>();

        private Transform _root;
        private PetBall _ball;

        /// <summary>
        /// The throwable ball. Looked up lazily rather than cached at build time, because a
        /// scene authored in the editor restores the ball from the hierarchy without ever
        /// running <see cref="BuildBall"/>.
        /// </summary>
        public PetBall Ball
        {
            get
            {
                if (_ball == null) _ball = GetComponentInChildren<PetBall>(true);
                return _ball;
            }
        }

        public void Build(bool editorMode)
        {
            var existing = transform.Find("Room");
            if (existing != null)
            {
                if (editorMode) DestroyImmediate(existing.gameObject);
                else Destroy(existing.gameObject);
            }

            Interactables.Clear();

            var root = new GameObject("Room");
            root.transform.SetParent(transform, false);
            _root = root.transform;

            ApplyTheme();
            BuildShell();
            BuildRug();
            BuildFurniture();

            BuildBowls();
            BuildBed();
            BuildBall();
            BuildBrush();
            BuildToilet();
            BuildBath();
            BuildDoor();

            BuildDecor();

            // Collected from the hierarchy rather than hand-registered, so the list is
            // correct whether the room was built now or restored from a saved scene.
            CollectInteractables();
        }

        public void CollectInteractables()
        {
            Interactables.Clear();
            foreach (var item in GetComponentsInChildren<Interactable>(true))
            {
                if (item != null) Interactables.Add(item);
            }
        }

        // -------------------------------------------------------------------- shell

        /// <summary>
        /// A wall or the ceiling, remembered with the direction it faces outward.
        ///
        /// The camera sits OUTSIDE the room and looks in — the only way to keep the whole room
        /// and both characters in frame while the HUD owns the bottom third of the screen. That
        /// only works if whatever is between the camera and the room is hidden, so each face
        /// can be switched off when the camera moves behind it.
        /// </summary>
        private struct ShellFace
        {
            public Renderer Renderer;
            public Vector3 Outward;
            public Vector3 Point;
        }

        private readonly List<ShellFace> _shellFaces = new List<ShellFace>();
        private bool _shellScanned;

        /// <summary>Wall object names under the "Room" root, used when rebuilding lazily.</summary>
        private static readonly string[] WallNames = { "WallNorth", "WallSouth", "WallEast", "WallWest" };

        private void AddFace(string name, Vector3 position, Vector3 size, Color color,
            float smoothness, Vector3 outward)
        {
            var box = Box(name, position, size, color, smoothness);
            var renderer = box.GetComponent<Renderer>();
            if (renderer != null)
            {
                _shellFaces.Add(new ShellFace
                {
                    Renderer = renderer,
                    Outward = outward.normalized,
                    Point = position
                });
            }
        }

        /// <summary>
        /// Culls the shell for a camera, dollhouse style: a wall is drawn only when the camera
        /// looks at its inner face.
        ///
        /// Two rules were tried here. "Hide what is between the camera and the room" keeps the
        /// near wall out of the way but leaves the side walls looming over a third of the
        /// frame, because from outside the room you see their inner faces edge-on. Showing only
        /// the wall the camera faces gives the consistent looking-into-a-room read — and the
        /// skirting boards stay, so the floor still has a border rather than floating.
        /// </summary>
        public void CullFacesForCamera(Transform camera)
        {
            if (camera == null) return;
            EnsureShellFaces();

            Vector3 view = camera.forward;
            view.y = 0f;
            if (view.sqrMagnitude < 0.0001f) view = Vector3.forward;
            view.Normalize();

            for (int i = 0; i < _shellFaces.Count; i++)
            {
                var face = _shellFaces[i];
                if (face.Renderer == null) continue;

                bool show = ShouldShowFace(face.Outward, view);
                if (face.Renderer.enabled != show) face.Renderer.enabled = show;
            }
        }

        /// <summary>
        /// The dollhouse rule, on its own so it can be tested without a scene: a wall is worth
        /// drawing only when the camera looks at its inner face, which means the wall's outward
        /// normal points roughly the same way the camera does.
        /// </summary>
        public static bool ShouldShowFace(Vector3 outward, Vector3 viewForward)
            => Vector3.Dot(outward, viewForward) > 0.5f;

        /// <summary>
        /// Finds the walls when <see cref="BuildShell"/> did not run.
        ///
        /// A room authored into the scene is restored from the hierarchy at runtime, so the
        /// face list — a runtime-only field — comes back empty and nothing gets culled, which
        /// leaves the near wall standing between the camera and the player. The list is rebuilt
        /// from the hierarchy by name, with the outward direction derived from the wall's
        /// offset from the room centre.
        /// </summary>
        private void EnsureShellFaces()
        {
            if (_shellScanned) return;
            _shellScanned = true;
            if (_shellFaces.Count > 0) return;

            var root = transform.Find("Room");
            if (root == null) return;

            for (int i = 0; i < WallNames.Length; i++)
            {
                var wall = root.Find(WallNames[i]);
                if (wall == null) continue;

                var renderer = wall.GetComponent<Renderer>();
                if (renderer == null) continue;

                Vector3 outward = wall.position - root.position;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.0001f) continue;

                _shellFaces.Add(new ShellFace
                {
                    Renderer = renderer,
                    Outward = outward.normalized,
                    Point = wall.position
                });
            }
        }

        /// <summary>Restores every face, so an editor preview is not left half-demolished.</summary>
        public void ShowAllFaces()
        {
            for (int i = 0; i < _shellFaces.Count; i++)
            {
                if (_shellFaces[i].Renderer != null) _shellFaces[i].Renderer.enabled = true;
            }
        }

        /// <summary>
        /// Which place this room is. Set before <see cref="Build"/>; the palette and the extra
        /// decor come from it, so "another scene" is a rebuild rather than another .unity file.
        ///
        /// Not serialized on purpose: the saved place lives in <see cref="PetWorldMap"/>, and a
        /// stale copy saved into the scene would silently override it after a move.
        /// </summary>
        [System.NonSerialized] public RoomTheme Theme = RoomTheme.Cabin;

        /// <summary>Applies the theme's palette to the fields the builders read.</summary>
        private void ApplyTheme()
        {
            var info = RoomThemeInfo.Get(Theme);
            FloorColor = info.Floor;
            WallColor = info.Wall;
            AccentWallColor = info.AccentWall;
            RugColor = info.Rug;

            // The place the geometry in this scene was actually built for. Serialized, unlike
            // Theme: at load time Theme is reset to the cabin, so this is the only way to tell
            // "the file already matches the save" from "the pet has moved since this was built"
            // — and without that, moving house and coming back left the pet in the old room.
            _builtTheme = Theme;

            // Paint alone reads as the same room with a different colour. The ambient tint is
            // what makes an evening terrace feel like evening and a garden feel like daylight.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(DefaultAmbient, info.Light * 0.55f, 0.9f);
        }

        /// <summary>The neutral ambient the scene builder writes; every place is a shift from it.</summary>
        private static readonly Color DefaultAmbient = new Color(0.42f, 0.40f, 0.46f);

        /// <summary>
        /// The place the geometry in this scene was built for, so a stale snapshot can be spotted.
        /// </summary>
        [SerializeField] private RoomTheme _builtTheme = RoomTheme.Cabin;

        /// <summary>True when the room in this scene is already the given place.</summary>
        public bool BuiltFor(RoomTheme theme) => _builtTheme == theme;

        private void BuildShell()
        {
            float half = Size * 0.5f;

            var floor = Box("Floor", new Vector3(0f, -0.15f, 0f), new Vector3(Size, 0.3f, Size), FloorColor, 0.55f);

            // Outward normals point away from the room centre.
            AddFace("WallNorth", new Vector3(0f, WallHeight * 0.5f, half), new Vector3(Size, WallHeight, 0.3f),
                WallColor, 0.5f, Vector3.forward);
            AddFace("WallWest", new Vector3(-half, WallHeight * 0.5f, 0f), new Vector3(0.3f, WallHeight, Size),
                WallColor, 0.5f, Vector3.left);
            AddFace("WallEast", new Vector3(half, WallHeight * 0.5f, 0f), new Vector3(0.3f, WallHeight, Size),
                WallColor, 0.5f, Vector3.right);
            // One accent wall so the room reads as designed rather than a grey box.
            AddFace("WallSouth", new Vector3(0f, WallHeight * 0.5f, -half), new Vector3(Size, WallHeight, 0.3f),
                AccentWallColor, 0.5f, Vector3.back);
            // No ceiling: the camera looks in from above, and an open top is the diorama look.

            // Skirting boards.
            var skirt = new Color(0.95f, 0.93f, 0.88f);
            Box("SkirtN", new Vector3(0f, 0.16f, half - 0.2f), new Vector3(Size, 0.32f, 0.12f), skirt, 0.4f);
            Box("SkirtS", new Vector3(0f, 0.16f, -half + 0.2f), new Vector3(Size, 0.32f, 0.12f), skirt, 0.4f);
            Box("SkirtW", new Vector3(-half + 0.2f, 0.16f, 0f), new Vector3(0.12f, 0.32f, Size), skirt, 0.4f);
            Box("SkirtE", new Vector3(half - 0.2f, 0.16f, 0f), new Vector3(0.12f, 0.32f, Size), skirt, 0.4f);

            _ = floor;
        }

        private void BuildRug()
        {
            Box("Rug", new Vector3(0f, 0.02f, 0.5f), new Vector3(6.5f, 0.04f, 5f), RugColor, 0.6f);
            Box("RugInner", new Vector3(0f, 0.035f, 0.5f), new Vector3(5.2f, 0.03f, 3.8f),
                Color.Lerp(RugColor, Color.white, 0.35f), 0.6f);
        }

        private void BuildFurniture()
        {
            // Low shelf along the back wall.
            Box("Shelf", new Vector3(-3.6f, 0.9f, 6.2f), new Vector3(3.6f, 1.8f, 0.7f),
                new Color(0.55f, 0.38f, 0.26f), 0.45f);
            Box("ShelfTop", new Vector3(-3.6f, 1.84f, 6.2f), new Vector3(3.9f, 0.12f, 0.9f),
                new Color(0.66f, 0.47f, 0.32f), 0.45f);
        }

        /// <summary>Decor that only makes sense in one place.</summary>
        private void BuildThemeDecor()
        {
            var info = RoomThemeInfo.Get(Theme);

            if (info.Theme == RoomTheme.Garden)
            {
                // A garden is mostly "more plants, fewer straight lines".
                SpawnProp("Runner/Nature/tree_pineRoundA", new Vector3(-5.4f, 0f, 5.4f), 2.1f);
                SpawnProp("Runner/Nature/tree_pineRoundB", new Vector3(5.6f, 0f, 5.0f), 1.9f);
                SpawnProp("Runner/Nature/flower_yellowA", new Vector3(-2.2f, 0f, 5.6f), 0.5f);
                SpawnProp("Runner/Nature/flower_redA", new Vector3(1.6f, 0f, 5.8f), 0.5f);
                SpawnProp("Runner/Nature/flower_purpleA", new Vector3(4.2f, 0f, 5.6f), 0.5f);
                SpawnProp("Runner/Nature/plant_bush", new Vector3(-5.9f, 0f, -1.2f), 1.1f);
                SpawnProp("Runner/Nature/mushroom_red", new Vector3(6.0f, 0f, -4.2f), 0.55f);
                SpawnRock(new Vector3(-4.6f, 0f, 2.2f), 0.7f);
                SpawnRock(new Vector3(4.8f, 0f, -3.4f), 0.55f);
            }
            else if (info.Theme == RoomTheme.Terrace)
            {
                // Lanterns: a warm point of light per corner, which is most of what makes a
                // night terrace read as one rather than as a dark room.
                SpawnLantern(new Vector3(-5.2f, 0f, 5.2f));
                SpawnLantern(new Vector3(5.2f, 0f, 5.2f));
                SpawnLantern(new Vector3(-5.2f, 0f, -5.2f));
                SpawnProp("Runner/Nature/plant_bush", new Vector3(5.8f, 0f, 1.4f), 0.9f);
            }
        }

        /// <summary>A grey stone, for the garden. Primitive rather than another downloaded prop.</summary>
        private void SpawnRock(Vector3 position, float size)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(_root, false);
            rock.transform.position = new Vector3(position.x, size * 0.28f, position.z);
            rock.transform.localScale = new Vector3(size * 1.3f, size * 0.62f, size);
            rock.transform.rotation = Quaternion.Euler(0f, position.x * 37f, 12f);
            SetColor(rock, new Color(0.55f, 0.56f, 0.58f), 0.2f);
            Destroy(rock.GetComponent<Collider>());
        }

        /// <summary>A lantern post with a glowing head.</summary>
        private void SpawnLantern(Vector3 position)
        {
            var root = new GameObject("Lantern");
            root.transform.SetParent(_root, false);
            root.transform.position = position;

            BoxUnder(root.transform, "Post", new Vector3(0f, 0.85f, 0f), new Vector3(0.14f, 1.7f, 0.14f),
                new Color(0.34f, 0.28f, 0.24f), 0.3f);
            BoxUnder(root.transform, "Head", new Vector3(0f, 1.82f, 0f), new Vector3(0.36f, 0.42f, 0.36f),
                new Color(1f, 0.86f, 0.52f), 0.1f);

            var light = new GameObject("Light");
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            var point = light.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = new Color(1f, 0.82f, 0.55f);
            point.range = 7f;
            point.intensity = 1.5f;
            point.shadows = LightShadows.None;
        }

        private void BuildDecor()
        {
            BuildThemeDecor();

            // Reuse the Kenney nature props that already ship for the runner scene.
            SpawnProp("Runner/Nature/tree_pineRoundA", new Vector3(-6.0f, 0f, 6.0f), 1.7f);
            SpawnProp("Runner/Nature/plant_bush", new Vector3(6.1f, 0f, 6.1f), 0.9f);
            SpawnProp("Runner/Nature/flower_yellowA", new Vector3(5.6f, 0f, -5.4f), 0.45f);
            SpawnProp("Runner/Nature/mushroom_red", new Vector3(-5.6f, 0f, -5.8f), 0.5f);
        }

        private void SpawnProp(string resourcePath, Vector3 position, float size)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return;

            var instance = Instantiate(prefab, _root);
            instance.name = System.IO.Path.GetFileName(resourcePath);

            var raw = MeasureBounds(instance);
            float max = Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
            if (max > 0.0001f) instance.transform.localScale = Vector3.one * (size / max);

            // Position, then measure, then lift so the prop's lowest point rests on the floor.
            // Measuring before the move is what left the tree and the bush hanging a metre and a
            // half in the air near the back wall: the correction was computed from bounds taken
            // at the prefab's own origin, not at the spot the prop was going to stand.
            instance.transform.position = new Vector3(position.x, 0f, position.z);
            var placed = MeasureBounds(instance);
            instance.transform.position = new Vector3(position.x, position.y - placed.min.y, position.z);

            foreach (var collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);

            TintProp(instance);
        }

        /// <summary>
        /// Gives a prop its own colour via a serialized <see cref="PropTint"/> component.
        ///
        /// The component matters: a material property block applied here would be lost the
        /// moment the room is saved into the scene and reloaded, since property blocks are not
        /// serialized.
        /// </summary>
        private static void TintProp(GameObject instance)
        {
            var tint = PropTint.ForName(instance.name);
            if (!tint.HasValue) return;

            var component = instance.GetComponent<PropTint>();
            if (component == null) component = instance.AddComponent<PropTint>();
            component.Tint = tint.Value;
            component.Apply();
        }

        /// <summary>
        /// World bounds, computed from the mesh bounds and the transform matrices.
        ///
        /// Deliberately not <c>Renderer.bounds</c>: a freshly instantiated prefab can report
        /// stale bounds in the frame it was created, and this measurement decides where the
        /// prop is placed.
        /// </summary>
        private static Bounds MeasureBounds(GameObject go)
        {
            bool any = false;
            var result = new Bounds(go.transform.position, Vector3.zero);

            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                var local = mesh.bounds;
                var matrix = filter.transform.localToWorldMatrix;
                var scale = matrix.lossyScale;
                var centre = matrix.MultiplyPoint3x4(local.center);
                var extents = new Vector3(
                    Mathf.Abs(local.extents.x * scale.x),
                    Mathf.Abs(local.extents.y * scale.y),
                    Mathf.Abs(local.extents.z * scale.z));

                var world = new Bounds(centre, extents * 2f);
                if (!any) { result = world; any = true; }
                else result.Encapsulate(world);
            }

            if (!any) return new Bounds(go.transform.position, Vector3.one);
            return result;
        }

        // -------------------------------------------------------------- interactables

        private Interactable[] BuildBowls()
        {            var food = Bowl("FoodBowl", new Vector3(3.4f, 0f, 3.2f), new Color(0.92f, 0.42f, 0.35f));
            food.Kind = InteractableKind.Food;
            food.Label = "食物碗";
            food.ApproachPoint = new Vector3(3.4f, 0f, 2.0f);
            AddFruit(food.transform, "Runner/Items/apple", new Vector3(0f, 0.18f, 0f), 0.26f);

            var water = Bowl("WaterBowl", new Vector3(4.6f, 0f, 3.2f), new Color(0.35f, 0.62f, 0.85f));
            water.Kind = InteractableKind.Water;
            water.Label = "水碗";
            water.ApproachPoint = new Vector3(4.6f, 0f, 2.0f);
            BoxUnder(water.transform, "Water", new Vector3(0f, 0.11f, 0f), new Vector3(0.62f, 0.06f, 0.62f),
                new Color(0.45f, 0.78f, 0.95f), 0.9f);

            return new[] { food, water };
        }

        private Interactable Bowl(string name, Vector3 position, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = position;

            var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cup.name = "Cup";
            cup.transform.SetParent(go.transform, false);
            cup.transform.localScale = new Vector3(0.8f, 0.09f, 0.8f);
            cup.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            SetColor(cup, color, 0.7f);

            var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "Inner";
            inner.transform.SetParent(go.transform, false);
            inner.transform.localScale = new Vector3(0.66f, 0.07f, 0.66f);
            inner.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            SetColor(inner, Color.Lerp(color, Color.black, 0.45f), 0.4f);

            // A generous invisible click volume so the small bowl is easy to hit.
            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(1.1f, 0.6f, 1.1f);
            hit.center = new Vector3(0f, 0.25f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Food;
            interactable.Label = name;
            return interactable;
        }

        private void AddFruit(Transform parent, string resourcePath, Vector3 localPosition, float size)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return;
            var fruit = Instantiate(prefab, parent);
            fruit.name = "Fruit";
            var bounds = MeasureBounds(fruit);
            float max = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (max > 0.0001f) fruit.transform.localScale = Vector3.one * (size / max);
            fruit.transform.localPosition = localPosition;
            foreach (var collider in fruit.GetComponentsInChildren<Collider>()) Destroy(collider);
        }

        private Interactable BuildBed()
        {
            var go = new GameObject("Bed");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(-4.4f, 0f, -4.4f);

            BoxUnder(go.transform, "Base", new Vector3(0f, 0.14f, 0f), new Vector3(2.2f, 0.28f, 1.8f),
                new Color(0.50f, 0.34f, 0.24f), 0.4f);
            BoxUnder(go.transform, "Cushion", new Vector3(0f, 0.34f, 0f), new Vector3(2.0f, 0.24f, 1.6f),
                new Color(0.85f, 0.55f, 0.62f), 0.6f);
            BoxUnder(go.transform, "Pillow", new Vector3(0f, 0.48f, -0.62f), new Vector3(0.9f, 0.16f, 0.42f),
                new Color(0.96f, 0.93f, 0.88f), 0.6f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(2.2f, 0.8f, 1.8f);
            hit.center = new Vector3(0f, 0.4f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Bed;
            interactable.Label = "小床";
            interactable.ApproachPoint = new Vector3(-4.4f, 0f, -2.9f);
            return interactable;
        }

        private Interactable BuildBall()
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(1.8f, 0f, 4.2f);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sphere";
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localScale = Vector3.one * 0.52f;
            sphere.transform.localPosition = new Vector3(0f, 0.26f, 0f);
            SetColor(sphere, new Color(0.95f, 0.72f, 0.22f), 0.85f);

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stripe.name = "Stripe";
            stripe.transform.SetParent(sphere.transform, false);
            stripe.transform.localScale = new Vector3(1.05f, 0.06f, 1.05f);
            stripe.transform.localPosition = Vector3.zero;
            SetColor(stripe, new Color(0.85f, 0.30f, 0.35f), 0.8f);

            // The clickable object is the ball root, so the collider has to live there too:
            // Unity delivers OnMouseDown to the collider's own GameObject, never to a parent.
            var hit = go.AddComponent<SphereCollider>();
            hit.radius = 0.34f;
            hit.center = new Vector3(0f, 0.26f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Ball;
            interactable.Label = "小球（按 E 拿起 / 点它让宠物去捡）";
            interactable.ApproachPoint = new Vector3(1.8f, 0f, 3.0f);

            // Throwing and fetching need the ball to be a stateful object, not a static prop.
            var ball = go.AddComponent<PetBall>();
            ball.SnapToRest(go.transform.position);
            return interactable;
        }

        private Interactable BuildBrush()
        {
            var go = new GameObject("Brush");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(-1.6f, 0f, 4.6f);

            BoxUnder(go.transform, "Handle", new Vector3(0f, 0.10f, 0f), new Vector3(0.9f, 0.09f, 0.16f),
                new Color(0.62f, 0.44f, 0.30f), 0.5f);
            BoxUnder(go.transform, "Bristles", new Vector3(0f, 0.055f, 0f), new Vector3(0.55f, 0.08f, 0.28f),
                new Color(0.90f, 0.88f, 0.80f), 0.5f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(1.1f, 0.5f, 0.7f);
            hit.center = new Vector3(0f, 0.15f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Brush;
            interactable.Label = "梳子";
            interactable.ApproachPoint = new Vector3(-1.6f, 0f, 3.4f);
            return interactable;
        }

        /// <summary>
        /// The litter tray. Deliberately in a corner: a pet that has to cross the room to
        /// reach it is a pet that sometimes does not make it, which is where the mess system
        /// comes from.
        /// </summary>
        private Interactable BuildToilet()
        {
            var go = new GameObject("Toilet");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(5.1f, 0f, -5.0f);

            BoxUnder(go.transform, "Tray", new Vector3(0f, 0.12f, 0f), new Vector3(1.7f, 0.24f, 1.3f),
                new Color(0.55f, 0.62f, 0.68f), 0.25f);
            BoxUnder(go.transform, "Litter", new Vector3(0f, 0.25f, 0f), new Vector3(1.5f, 0.10f, 1.1f),
                new Color(0.86f, 0.82f, 0.70f), 0.9f);
            BoxUnder(go.transform, "Lip", new Vector3(0f, 0.30f, -0.62f), new Vector3(1.7f, 0.14f, 0.08f),
                new Color(0.45f, 0.52f, 0.58f), 0.3f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(1.7f, 0.6f, 1.3f);
            hit.center = new Vector3(0f, 0.3f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Toilet;
            interactable.Label = "猫砂盆";
            interactable.ApproachPoint = new Vector3(5.1f, 0f, -3.6f);
            return interactable;
        }

        /// <summary>A wash basin with a raised lip, plus a soap bar so it reads as a bath.</summary>
        private Interactable BuildBath()
        {
            var go = new GameObject("Bath");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(-5.2f, 0f, 4.6f);

            BoxUnder(go.transform, "Tub", new Vector3(0f, 0.26f, 0f), new Vector3(1.9f, 0.52f, 1.5f),
                new Color(0.80f, 0.86f, 0.90f), 0.35f);
            BoxUnder(go.transform, "Water", new Vector3(0f, 0.50f, 0f), new Vector3(1.7f, 0.10f, 1.3f),
                new Color(0.45f, 0.72f, 0.86f), 0.9f);
            BoxUnder(go.transform, "Soap", new Vector3(0.62f, 0.58f, 0.42f), new Vector3(0.34f, 0.14f, 0.22f),
                new Color(0.98f, 0.80f, 0.86f), 0.7f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(1.9f, 0.8f, 1.5f);
            hit.center = new Vector3(0f, 0.4f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Bath;
            interactable.Label = "澡盆";
            interactable.ApproachPoint = new Vector3(-5.2f, 0f, 3.4f);
            return interactable;
        }

        // ---------------------------------------------------------------------- messes

        /// <summary>A puddle left after an accident. Lives in its own list so the room can
        /// report "there is something to clean up" without scanning everything.</summary>
        public readonly List<Interactable> Messes = new List<Interactable>();

        private Transform _messRoot;

        /// <summary>
        /// Leaves a mess at a position. Idempotent per position: a pet that has two accidents
        /// in the same corner should not stack up overlapping puddles.
        /// </summary>
        public Interactable SpawnMess(Vector3 position)
        {
            if (_messRoot == null)
            {
                var root = new GameObject("Messes");
                root.transform.SetParent(_root != null ? _root : transform, false);
                _messRoot = root.transform;
            }

            for (int i = 0; i < Messes.Count; i++)
            {
                if (Messes[i] == null) continue;
                if (Vector3.Distance(Messes[i].transform.position, position) < 1.2f) return Messes[i];
            }

            var go = new GameObject("Puddle");
            go.transform.SetParent(_messRoot, false);
            go.transform.position = new Vector3(position.x, 0f, position.z);

            // A flattened disc reads as a puddle at this camera angle where a sphere would
            // read as an object the pet could pick up.
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Stain";
            disc.transform.SetParent(go.transform, false);
            disc.transform.localScale = new Vector3(0.86f, 0.012f, 0.62f);
            disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            SetColor(disc, new Color(0.78f, 0.72f, 0.42f), 0f);
            Destroy(disc.GetComponent<Collider>());

            var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drop.name = "Drop";
            drop.transform.SetParent(go.transform, false);
            drop.transform.localScale = Vector3.one * 0.16f;
            drop.transform.localPosition = new Vector3(0.28f, 0.05f, -0.16f);
            SetColor(drop, new Color(0.72f, 0.66f, 0.38f), 0f);
            Destroy(drop.GetComponent<Collider>());

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(1.1f, 0.35f, 0.9f);
            hit.center = new Vector3(0f, 0.15f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Mess;
            interactable.Label = "地上的污渍（按 E 擦掉）";
            interactable.ApproachPoint = new Vector3(position.x, 0f, position.z - 1.1f);
            Messes.Add(interactable);
            return interactable;
        }

        /// <summary>Removes a cleaned-up mess from the room.</summary>
        public void RemoveMess(Interactable mess)
        {
            if (mess == null) return;
            Messes.Remove(mess);
            if (Application.isPlaying) Destroy(mess.gameObject);
            else DestroyImmediate(mess.gameObject);
        }

        public bool HasMess
        {
            get
            {
                for (int i = 0; i < Messes.Count; i++)
                {
                    if (Messes[i] != null) return true;
                }
                return false;
            }
        }

        public Interactable NearestMess(Vector3 from)
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Messes.Count; i++)
            {
                var mess = Messes[i];
                if (mess == null) continue;
                float distance = Vector3.Distance(from, mess.transform.position);
                if (distance < bestDistance) { bestDistance = distance; best = mess; }
            }
            return best;
        }

        /// <summary>
        /// The door to the rest of the world. It is an Interactable like everything else, but
        /// the click is aimed at the player rather than the pet: walking up to it and pressing
        /// E opens the activity chooser.
        /// </summary>
        private Interactable BuildDoor()
        {
            float half = Size * 0.5f;
            var go = new GameObject("Door");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(0f, 0f, half - 0.28f);

            var frameColor = new Color(0.58f, 0.42f, 0.30f);
            float doorW = 1.5f, doorH = 2.6f;

            BoxUnder(go.transform, "FrameL", new Vector3(-doorW * 0.5f - 0.08f, doorH * 0.5f, 0f),
                new Vector3(0.16f, doorH, 0.3f), frameColor, 0.7f);
            BoxUnder(go.transform, "FrameR", new Vector3(doorW * 0.5f + 0.08f, doorH * 0.5f, 0f),
                new Vector3(0.16f, doorH, 0.3f), frameColor, 0.7f);
            BoxUnder(go.transform, "FrameTop", new Vector3(0f, doorH + 0.08f, 0f),
                new Vector3(doorW + 0.32f, 0.16f, 0.3f), frameColor, 0.7f);

            var panel = BoxUnder(go.transform, "Panel", new Vector3(0f, doorH * 0.5f, 0.02f),
                new Vector3(doorW, doorH, 0.12f), new Color(0.44f, 0.62f, 0.72f), 0.75f);

            // Details hang off the door root, not the panel: the panel is a scaled cube, so
            // anything parented to it would inherit that non-uniform scale.
            BoxUnder(go.transform, "Window", new Vector3(0f, doorH * 0.68f, 0.10f),
                new Vector3(0.62f, 0.46f, 0.06f), new Color(0.86f, 0.94f, 0.99f), 1.1f);
            BoxUnder(go.transform, "Handle", new Vector3(0.36f, doorH * 0.44f, 0.11f),
                new Vector3(0.11f, 0.11f, 0.16f), new Color(0.92f, 0.82f, 0.45f), 1f);
            _ = panel;

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(doorW + 0.4f, doorH, 0.6f);
            hit.center = new Vector3(0f, doorH * 0.5f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Door;
            interactable.Label = "门（出去走走）";
            interactable.ApproachPoint = new Vector3(0f, 0f, half - 1.6f);
            interactable.CooldownSeconds = 0.5f;
            return interactable;
        }

        // ------------------------------------------------------------------- helpers

        private GameObject Box(string name, Vector3 position, Vector3 scale, Color color, float emission)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            SetColor(go, color, emission);
            StripCollider(go);
            return go;
        }

        private static GameObject BoxUnder(Transform parent, string name, Vector3 localPosition, Vector3 scale,
            Color color, float emission)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            SetColor(go, color, emission);
            StripCollider(go);
            return go;
        }

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        /// <summary>
        /// DSH/Neon is unlit — _Emission *is* the surface brightness, so the per-call
        /// emission values below are really "how lit is this" fractions. The boost lifts
        /// the whole room into a daylit range without touching every call site.
        /// </summary>
        private const float BrightnessBoost = 1.7f;

        private static void SetColor(GameObject go, Color color, float emission)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            float brightness = Mathf.Clamp01(emission * BrightnessBoost);
            string key = ColorUtility.ToHtmlStringRGB(color) + "_" + brightness.ToString("F2");
            if (!Cache.TryGetValue(key, out var material) || material == null)
            {
                var shader = Shader.Find("DSH/Neon") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "Room_" + key };
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Emission")) material.SetFloat("_Emission", brightness);
                if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", 0.18f);
                if (material.HasProperty("_RimPower")) material.SetFloat("_RimPower", 3f);
                Cache[key] = material;
            }
            renderer.sharedMaterial = material;
        }

        private static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
        }
    }
}
