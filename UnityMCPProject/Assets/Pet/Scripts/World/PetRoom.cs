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
            BuildPurchasedProps();
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

            // Music follows the *place*, not the scene: PetRoom is one scene whether the pet lives
            // in a cabin, a garden or on a night terrace, and the scene-name wiring cannot tell
            // them apart. Asking here means moving house changes the music, which is most of what
            // makes it feel like moving.
            DshMobile.MobileMusic.PlayForTheme(Theme);
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
            var info = RoomThemeInfo.Get(Theme);

            // Three places, three kinds of boundary. This is the difference the first version of
            // the map missed completely: a garden and a terrace built out of the cabin's four
            // walls are the same room twice, however carefully they are painted.
            switch (info.Shell)
            {
                case RoomShell.Fenced:
                    BuildGardenShell(info);
                    break;
                case RoomShell.Railed:
                    BuildTerraceShell(info);
                    break;
                default:
                    BuildCabinShell();
                    break;
            }
        }

        private void BuildCabinShell()
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

        /// <summary>
        /// A lawn with a picket fence around it.
        ///
        /// No walls at all, and a skirt of grass four times the lawn's size outside the fence: the
        /// camera in this scene renders a solid dark background, so a garden that stopped at the
        /// fence would be a green raft floating in the void — which is exactly what the first
        /// attempt at an outdoor place looked like.
        /// </summary>
        private void BuildGardenShell(RoomThemeInfo info)
        {
            float half = Size * 0.5f;

            Box("GrassSkirt", new Vector3(0f, -0.22f, 0f),
                new Vector3(Size * 4.5f, 0.32f, Size * 4.5f), RoomDecor.Grass * 0.62f, 0.5f);

            var lawn = Box("Lawn", new Vector3(0f, -0.13f, 0f), new Vector3(Size, 0.28f, Size),
                RoomDecor.Grass, 0.55f);
            SetTextured(lawn, Color.white, RoomTextures.Grass(), new Vector2(Size / 3.5f, Size / 3.5f));

            // A mown edge where the flower beds stop and the lawn starts: a slightly darker band
            // just inside the fence, which also hides the fence's own shadow line.
            var edge = RoomDecor.Grass * 0.78f;
            float inset = half - 0.7f;
            Box("BedNorth", new Vector3(0f, 0.0f, inset), new Vector3(Size - 1.2f, 0.06f, 1.1f), edge, 0.5f);
            Box("BedSouth", new Vector3(0f, 0.0f, -inset), new Vector3(Size - 1.2f, 0.06f, 1.1f), edge, 0.5f);
            Box("BedWest", new Vector3(-inset, 0.0f, 0f), new Vector3(1.1f, 0.06f, Size - 1.2f), edge, 0.5f);
            Box("BedEast", new Vector3(inset, 0.0f, 0f), new Vector3(1.1f, 0.06f, Size - 1.2f), edge, 0.5f);

            BuildFence(info, 1.35f);
        }

        /// <summary>
        /// A stone deck with a balustrade, a pergola at the far end and the city beyond it.
        ///
        /// The skyline panels are the trick: an outdoor place needs a horizon, and this scene's
        /// camera clears to a dark colour, which is precisely what a night city wants. Three
        /// panels — behind, left and right — are enough, because the camera only ever looks in
        /// from the front.
        /// </summary>
        private void BuildTerraceShell(RoomThemeInfo info)
        {
            float half = Size * 0.5f;

            // The deck stands on a slab that runs a little past the railing: a roof edge.
            var deck = Box("Deck", new Vector3(0f, -0.13f, 0f), new Vector3(Size, 0.28f, Size), info.Floor, 0.5f);
            SetTextured(deck, Color.white, RoomTextures.Deck(), new Vector2(Size / 4f, Size / 4f));

            Box("Parapet", new Vector3(0f, -0.35f, 0f), new Vector3(Size + 2.4f, 0.46f, Size + 2.4f),
                info.Floor * 0.62f, 0.4f);

            BuildRailing(info, 0.78f);
            BuildSkyline();

            // Warm light along the deck: without it a night place is only dark.
            var glow = new Color(1f, 0.80f, 0.52f);
            Box("DeckGlowN", new Vector3(0f, 0.01f, half - 1.1f), new Vector3(Size - 1.6f, 0.03f, 1.4f),
                glow * 0.35f, 0.7f);
        }

        /// <summary>
        /// A picket fence around the lawn.
        ///
        /// Four cutout panels (see <see cref="RoomTextures.Pickets"/>) held up by real posts and a
        /// real top rail, which is what makes it read as a built fence rather than as wallpaper:
        /// the posts have depth and catch the light, the pickets between them are drawn.
        /// </summary>
        private void BuildFence(RoomThemeInfo info, float spacing)
        {
            float half = Size * 0.5f;
            var wood = info.Wood;
            var fence = new GameObject("Fence");
            fence.transform.SetParent(_root, false);

            var sides = new[]
            {
                new { Name = "North", Centre = new Vector3(0f, 0f, half), Size = new Vector2(Size, 0f), Along = true },
                new { Name = "South", Centre = new Vector3(0f, 0f, -half), Size = new Vector2(Size, 0f), Along = true },
                new { Name = "East", Centre = new Vector3(half, 0f, 0f), Size = new Vector2(0f, Size), Along = false },
                new { Name = "West", Centre = new Vector3(-half, 0f, 0f), Size = new Vector2(0f, Size), Along = false }
            };

            for (int i = 0; i < sides.Length; i++)
            {
                var side = sides[i];
                float length = side.Along ? side.Size.x : side.Size.y;

                // The panel: 0.98 m tall, its pattern repeating every half metre.
                Panel(fence.transform, "FencePanel" + side.Name,
                    side.Centre + new Vector3(0f, 0.49f, 0f),
                    new Vector2(length, 0.98f),
                    RoomTextures.Pickets(),
                    side.Along ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.Euler(0f, side.Centre.x > 0 ? -90f : 90f, 0f),
                    wood, length / 0.5f);

                // A top rail in front of the pickets, so the fence has an edge.
                BoxUnder(fence.transform, "FenceRail" + side.Name,
                    side.Centre + new Vector3(0f, 0.98f, 0f),
                    side.Along ? new Vector3(length, 0.09f, 0.09f) : new Vector3(0.09f, 0.09f, length),
                    wood, 0.5f);

                // Posts at the corners and every so often along the run.
                int posts = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(1f, spacing)));
                for (int p = 0; p <= posts; p++)
                {
                    float t = -half + length * (p / (float)posts);
                    var at = side.Along
                        ? new Vector3(t, 0f, side.Centre.z)
                        : new Vector3(side.Centre.x, 0f, t);

                    BoxUnder(fence.transform, "FencePost" + side.Name + p, new Vector3(at.x, 0.62f, at.z),
                        new Vector3(0.16f, 1.24f, 0.16f), wood, 0.5f);
                    BoxUnder(fence.transform, "FenceCap" + side.Name + p, new Vector3(at.x, 1.29f, at.z),
                        new Vector3(0.20f, 0.10f, 0.20f), wood * 1.08f, 0.5f);
                }
            }
        }

        /// <summary>A balustrade: a stone-grey hand rail, a foot rail, and drawn balusters.</summary>
        private void BuildRailing(RoomThemeInfo info, float spacing)
        {
            float half = Size * 0.5f;
            var metal = info.Wood;
            var rail = new GameObject("Railing");
            rail.transform.SetParent(_root, false);

            var sides = new[]
            {
                new { Name = "North", Centre = new Vector3(0f, 0f, half), Along = true },
                new { Name = "South", Centre = new Vector3(0f, 0f, -half), Along = true },
                new { Name = "East", Centre = new Vector3(half, 0f, 0f), Along = false },
                new { Name = "West", Centre = new Vector3(-half, 0f, 0f), Along = false }
            };

            for (int i = 0; i < sides.Length; i++)
            {
                var side = sides[i];
                float length = Size;
                var rotation = side.Along
                    ? Quaternion.Euler(0f, 180f, 0f)
                    : Quaternion.Euler(0f, side.Centre.x > 0 ? -90f : 90f, 0f);

                Panel(rail.transform, "BalusterPanel" + side.Name,
                    side.Centre + new Vector3(0f, 0.60f, 0f),
                    new Vector2(length, 0.96f), RoomTextures.Balusters(), rotation, metal * 1.25f, length / 0.62f);

                BoxUnder(rail.transform, "RailTop" + side.Name, side.Centre + new Vector3(0f, 1.12f, 0f),
                    side.Along ? new Vector3(length, 0.10f, 0.16f) : new Vector3(0.16f, 0.10f, length),
                    metal, 0.5f);
                BoxUnder(rail.transform, "RailFoot" + side.Name, side.Centre + new Vector3(0f, 0.14f, 0f),
                    side.Along ? new Vector3(length, 0.08f, 0.12f) : new Vector3(0.12f, 0.08f, length),
                    metal, 0.4f);

                int posts = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(1f, spacing)));
                for (int p = 0; p <= posts; p++)
                {
                    float t = -half + length * (p / (float)posts);
                    var at = side.Along
                        ? new Vector3(t, 0f, side.Centre.z)
                        : new Vector3(side.Centre.x, 0f, t);

                    BoxUnder(rail.transform, "RailPost" + side.Name + p, new Vector3(at.x, 0.58f, at.z),
                        new Vector3(0.17f, 1.16f, 0.17f), metal * 1.05f, 0.45f);
                    BoxUnder(rail.transform, "RailCap" + side.Name + p, new Vector3(at.x, 1.20f, at.z),
                        new Vector3(0.23f, 0.09f, 0.23f), metal * 1.2f, 0.5f);
                }
            }
        }

        /// <summary>
        /// A cutout panel: a quad whose texture carries its own alpha, facing the camera side.
        ///
        /// Sprites/Default because it is unlit (the fence is whittled wood in daylight, not a
        /// metal surface catching a light) and two-sided, so the same panel is visible from
        /// inside the garden and from the camera outside it.
        /// </summary>
        private GameObject Panel(Transform parent, string name, Vector3 position, Vector2 size,
            Texture2D texture, Quaternion rotation, Color tint, float repeat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            if (quad == null) return null;

            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.position = position;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.transform.rotation = rotation;
            StripCollider(quad);

            var renderer = quad.GetComponent<Renderer>();
            if (renderer == null) return quad;

            renderer.sharedMaterial = CutoutMaterial(texture, tint, repeat);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return quad;
        }

        /// <summary>Cutout materials, cached per texture and tiling: a garden is a hundred panels
        /// made of eight textures, and one material each would be a leak and a hundred batches.</summary>
        private static readonly Dictionary<string, Material> CutoutCache = new Dictionary<string, Material>();

        private static Material CutoutMaterial(Texture2D texture, Color tint, float repeat)
        {
            if (texture == null) return null;

            string key = texture.GetInstanceID() + "_" + ColorUtility.ToHtmlStringRGB(tint) + "_" +
                         repeat.ToString("F2");
            if (CutoutCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (shader == null) return null;

            var material = new Material(shader) { name = "Room_cut_" + key, mainTexture = texture };
            material.mainTextureScale = new Vector2(Mathf.Max(1f, repeat), 1f);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);

            CutoutCache[key] = material;
            return material;
        }

        /// <summary>The city panels beyond the railing: one behind and one each side.</summary>
        private void BuildSkyline()
        {
            float half = Size * 0.5f;
            float distance = half + 5.4f;

            var texture = RoomTextures.Skyline();
            var root = new GameObject("Skyline");
            root.transform.SetParent(_root, false);
            var night = new Color(0.80f, 0.84f, 1f);

            // Three panels, not four: the camera looks in from the front, and a panel behind the
            // camera would be the one thing on the terrace it could never see.
            Panel(root.transform, "SkylineNorth", new Vector3(0f, 4.6f, distance),
                new Vector2(distance * 4f, 9.2f), texture, Quaternion.Euler(0f, 180f, 0f), night,
                distance * 4f / 24f);
            Panel(root.transform, "SkylineWest", new Vector3(-distance, 4.6f, 0f),
                new Vector2(distance * 4f, 9.2f), texture, Quaternion.Euler(0f, 90f, 0f), night,
                distance * 4f / 24f);
            Panel(root.transform, "SkylineEast", new Vector3(distance, 4.6f, 0f),
                new Vector2(distance * 4f, 9.2f), texture, Quaternion.Euler(0f, -90f, 0f), night,
                distance * 4f / 24f);
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

            switch (info.Shell)
            {
                case RoomShell.Fenced:
                    BuildGardenDecor(info);
                    return;
                case RoomShell.Railed:
                    BuildTerraceDecor(info);
                    return;
            }

            // The cabin's own touches: a couple of pot plants and a mushroom by the skirting.
            SpawnProp("Runner/Nature/plant_bush", new Vector3(6.1f, 0f, 6.1f), 0.9f);
            SpawnProp("Runner/Nature/flower_yellowA", new Vector3(5.6f, 0f, -5.4f), 0.45f);
            SpawnProp("Runner/Nature/mushroom_red", new Vector3(-5.6f, 0f, -5.8f), 0.5f);
        }

        /// <summary>
        /// The garden: seven beds of flowers, a stone path, tufts of long grass, and three shrubs
        /// to hide behind.
        ///
        /// The flowers are crossed cutout panels (<see cref="RoomTextures.Flower"/>) rather than
        /// modelled petals: 「种满五颜六色的花」 needs *a lot* of flowers, and forty flowers built
        /// out of spheres would be four hundred objects on a phone. Each flower is two quads that
        /// are still there when the camera swings round, in one of eight colours, and the whole
        /// garden costs about a hundred objects.
        /// </summary>
        private void BuildGardenDecor(RoomThemeInfo info)
        {
            float half = Size * 0.5f;
            var keepOut = RoomDecor.FurnitureKeepOut();
            var beds = RoomDecor.Beds(Size);
            var flowers = new GameObject("Flowers");
            flowers.transform.SetParent(_root, false);

            for (int i = 0; i < beds.Length; i++)
            {
                var bed = beds[i];

                // Turned earth under the flowers, so a bed reads as planted rather than sprinkled.
                Prim(PrimitiveType.Cylinder, "FlowerBed" + i, _root,
                    new Vector3(bed.Centre.x, 0.035f, bed.Centre.y),
                    new Vector3(bed.Radius * 2f, 0.035f, bed.Radius * 2f), RoomDecor.Soil, 0.35f);

                var plans = RoomDecor.Flowers(bed, 9173 + i * 613, keepOut, 1.15f, 0.40f, 9);
                for (int f = 0; f < plans.Length; f++) BuildFlower(flowers.transform, plans[f], "F" + i + "_" + f);
            }

            // Long grass along the beds: the edge of a lawn is never a clean line.
            var rng = new System.Random(31337);
            var tufts = new GameObject("Tufts");
            tufts.transform.SetParent(_root, false);
            for (int i = 0; i < 26; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = half - 0.5f - (float)rng.NextDouble() * 0.9f;
                var at = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (RoomDecor.Blocked(new Vector2(at.x, at.z), keepOut, 0.9f)) continue;

                Cutout(tufts.transform, "Tuft" + i, at, 0.42f, RoomTextures.GrassTuft(4200 + i),
                    new Color(0.9f, 1f, 0.85f), 1f, (float)rng.NextDouble() * 180f);
            }

            // The path from the door to the middle: flat stones, wobbled so it is a path and not a ruler.
            var stones = RoomDecor.SteppingStones(new Vector2(0f, half - 1.1f), new Vector2(0f, 0.6f), 6, 5150);
            for (int i = 0; i < stones.Length; i++)
            {
                Prim(PrimitiveType.Cylinder, "Stone" + i, _root,
                    new Vector3(stones[i].x, 0.03f, stones[i].y),
                    new Vector3(0.72f, 0.03f, 0.72f), RoomDecor.Path, 0.45f);
            }

            // Three shrubs to hide behind, spaced around the lawn.
            SpawnHidingSpot(new Vector3(-half + 2.0f, 0f, 1.9f), 1.15f, "灌木");
            SpawnHidingSpot(new Vector3(half - 2.1f, 0f, -1.7f), 1.05f, "花丛");
            SpawnHidingSpot(new Vector3(half - 4.4f, 0f, half - 2.1f), 1.2f, "大树后");

            // Outside the fence: the garden does not end at the pickets. A ring of varied Kenney
            // trees, rocks, stumps and mushrooms gives the garden depth instead of a flat grass
            // skirt — the first pass had two of one tree and a missing one (tree_pineRoundB was
            // never in the pack), which is exactly the "still too sparse" read.
            SpawnProp("Runner/Nature/tree_oak", new Vector3(-9.6f, 0f, 7.8f), 3.6f);
            SpawnProp("Runner/Nature/tree_pineTallA", new Vector3(10.2f, 0f, 8.2f), 3.4f);
            SpawnProp("Runner/Nature/tree_pineRoundA", new Vector3(-11.4f, 0f, -4.6f), 3.0f);
            SpawnProp("Runner/Nature/tree_default", new Vector3(11.8f, 0f, -5.4f), 2.9f);
            SpawnProp("Runner/Nature/tree_simple", new Vector3(-12.8f, 0f, 3.2f), 2.6f);
            SpawnProp("Runner/Nature/tree_fat", new Vector3(12.6f, 0f, 2.8f), 2.4f);

            SpawnProp("Runner/Nature/plant_bush", new Vector3(9.0f, 0f, -7.0f), 1.5f);
            SpawnProp("Runner/Nature/plant_bushLarge", new Vector3(-8.2f, 0f, -8.8f), 1.8f);
            SpawnProp("Runner/Nature/plant_bush", new Vector3(-9.4f, 0f, 8.8f), 1.3f);

            SpawnProp("Runner/Nature/rock_largeA", new Vector3(-7.4f, 0f, 6.6f), 1.0f);
            SpawnProp("Runner/Nature/rock_smallA", new Vector3(7.6f, 0f, 7.0f), 0.6f);
            SpawnProp("Runner/Nature/rock_tallA", new Vector3(8.8f, 0f, -6.6f), 0.9f);
            SpawnProp("Runner/Nature/stone_tallA", new Vector3(-8.8f, 0f, -6.0f), 0.9f);

            SpawnProp("Runner/Nature/stump_round", new Vector3(-7.8f, 0f, -7.8f), 0.8f);
            SpawnProp("Runner/Nature/log", new Vector3(8.6f, 0f, 8.6f), 1.1f);
            SpawnProp("Runner/Nature/mushroom_tan", new Vector3(6.6f, 0f, -7.8f), 0.5f);
            SpawnProp("Runner/Nature/mushroom_red", new Vector3(-8.6f, 0f, 8.0f), 0.5f);

            // A few accents inside the lawn, kept off the walk and the furniture.
            SpawnProp("Runner/Nature/rock_smallD", new Vector3(-5.6f, 0f, 2.4f), 0.55f);
            SpawnProp("Runner/Nature/stump_square", new Vector3(5.8f, 0f, 1.2f), 0.7f);
            SpawnProp("Runner/Nature/grass_large", new Vector3(-5.4f, 0f, -2.2f), 0.9f);
            SpawnProp("Runner/Nature/grass", new Vector3(5.6f, 0f, -2.6f), 0.7f);
            SpawnRock(new Vector3(-6.2f, 0f, -5.6f), 0.8f);
            SpawnRock(new Vector3(6.8f, 0f, 5.9f), 0.6f);
        }

        /// <summary>One flower: two crossed cutout panels, so it survives the camera moving.</summary>
        private void BuildFlower(Transform parent, FlowerPlan plan, string name)
        {
            var texture = RoomTextures.Flower(plan.Colour, plan.Petals);
            float height = Mathf.Max(0.24f, plan.Height) * 1.35f;

            var root = new GameObject("Flower" + name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(plan.At.x, 0f, plan.At.y);

            for (int i = 0; i < 2; i++)
            {
                Cutout(root.transform, "Face" + i, Vector3.zero, height, texture, Color.white,
                    height * 0.72f, plan.Turn + i * 90f, 0.5f * height);
            }
        }

        /// <summary>
        /// A cutout panel standing on the ground: the flower, the grass tuft, the pickets.
        ///
        /// <paramref name="width"/> is the panel's width, <paramref name="lift"/> its centre above
        /// the floor — the texture has the stem at the bottom, so the quad has to sit half its
        /// height up or the flower is planted at ankle depth.
        /// </summary>
        private GameObject Cutout(Transform parent, string name, Vector3 at, float height, Texture2D texture,
            Color tint, float width, float turn, float lift = 0f)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            if (quad == null) return null;

            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.position = new Vector3(at.x, at.y + (lift > 0f ? lift : height * 0.5f), at.z);
            quad.transform.localScale = new Vector3(width, height, 1f);
            quad.transform.rotation = Quaternion.Euler(0f, turn, 0f);
            StripCollider(quad);

            var renderer = quad.GetComponent<Renderer>();
            if (renderer == null) return quad;

            renderer.sharedMaterial = CutoutMaterial(texture, tint, 1f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad;
        }

        /// <summary>
        /// A shrub with an <see cref="InteractableKind.HidingSpot"/> on it, positioned so the pet
        /// stands on the far side: hide-and-seek only works if the bush is between the pet and
        /// whoever is looking.
        /// </summary>
        private Interactable SpawnHidingSpot(Vector3 position, float size, string label)
        {
            var go = new GameObject("HidingSpot");
            go.transform.SetParent(_root, false);
            go.transform.position = position;

            var dark = new Color(0.20f, 0.42f, 0.20f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 2.1f;
                var blob = Prim(PrimitiveType.Sphere, "Leaf" + i, go.transform,
                    new Vector3(Mathf.Cos(angle) * size * 0.28f, size * (0.42f + i * 0.16f), Mathf.Sin(angle) * size * 0.28f),
                    Vector3.one * size * (0.95f - i * 0.12f), Color.Lerp(dark, Color.white, i * 0.05f), 0.45f);
                blob.transform.localScale = new Vector3(size * 0.95f, size * 0.8f, size * 0.95f);
            }

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(size * 1.1f, size * 1.2f, size * 1.1f);
            hit.center = new Vector3(0f, size * 0.6f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.HidingSpot;
            interactable.Label = label;
            interactable.CooldownSeconds = 12f;

            // The far side of the bush from the middle of the garden.
            var outward = new Vector3(position.x, 0f, position.z);
            if (outward.sqrMagnitude < 0.001f) outward = Vector3.back;
            interactable.ApproachPoint = position + outward.normalized * (size * 0.95f);
            return interactable;
        }

        /// <summary>
        /// The terrace: a pergola with lights strung under it, places to sit, planters, and a
        /// brazier. What makes it read as somewhere rather than as a blue room is that none of
        /// the furniture is against a wall — there are no walls.
        /// </summary>
        private void BuildTerraceDecor(RoomThemeInfo info)
        {
            float half = Size * 0.5f;
            var metal = info.Wood;
            var warm = new Color(1f, 0.84f, 0.55f);

            // Pergola along the far edge: posts, two beams, and slats that let the camera see
            // through — a solid roof would black out the whole back half of the deck.
            var pergola = new GameObject("Pergola");
            pergola.transform.SetParent(_root, false);
            float z = half - 0.9f;
            for (int i = 0; i < 4; i++)
            {
                float x = -half + 1.2f + i * ((Size - 2.4f) / 3f);
                BoxUnder(pergola.transform, "Column" + i, new Vector3(x, 1.35f, z),
                    new Vector3(0.16f, 2.7f, 0.16f), metal * 1.35f, 0.45f);
            }
            BoxUnder(pergola.transform, "BeamFront", new Vector3(0f, 2.62f, z - 0.75f),
                new Vector3(Size - 1.4f, 0.12f, 0.14f), metal * 1.4f, 0.5f);
            BoxUnder(pergola.transform, "BeamBack", new Vector3(0f, 2.62f, z + 0.75f),
                new Vector3(Size - 1.4f, 0.12f, 0.14f), metal * 1.4f, 0.5f);
            for (int i = 0; i < 13; i++)
            {
                float x = -half + 0.9f + i * ((Size - 1.8f) / 12f);
                BoxUnder(pergola.transform, "Slat" + i, new Vector3(x, 2.66f, z),
                    new Vector3(0.07f, 0.06f, 1.6f), metal * 1.45f, 0.5f);
            }

            // String lights under the pergola, sagging between the two beams.
            BuildStringLights(new Vector3(-half + 1.2f, 2.45f, z - 0.6f),
                new Vector3(half - 1.2f, 2.45f, z - 0.6f), 13, 0.45f, warm);
            BuildStringLights(new Vector3(-half + 1.2f, 2.45f, z + 0.6f),
                new Vector3(half - 1.2f, 2.45f, z + 0.6f), 13, 0.45f, warm);

            // The lounge, now real Kenney furniture instead of box-built loungers. A sofa, a
            // longer sofa, a coffee table, and a rug they sit on.
            SpawnProp("Kenney/Furniture/loungeSofa", new Vector3(-2.8f, 0f, 1.9f), 2.4f);
            SpawnProp("Kenney/Furniture/loungeSofaLong", new Vector3(2.9f, 0f, 1.9f), 2.6f);
            SpawnProp("Kenney/Furniture/tableCoffee", new Vector3(0f, 0f, 2.4f), 1.2f);
            SpawnProp("Kenney/Furniture/rugRound", new Vector3(0f, 0f, 2.0f), 2.6f);

            // Potted plants along the railing, and a floor lamp to warm the corner.
            SpawnProp("Kenney/Furniture/pottedPlant", new Vector3(-half + 1.2f, 0f, -half + 1.7f), 1.0f);
            SpawnProp("Kenney/Furniture/pottedPlant", new Vector3(half - 1.2f, 0f, -half + 1.7f), 1.0f);
            SpawnProp("Kenney/Furniture/plantSmall2", new Vector3(-half + 1.2f, 0f, 0.5f), 0.8f);
            SpawnProp("Kenney/Furniture/plantSmall3", new Vector3(half - 1.2f, 0f, 0.5f), 0.8f);
            SpawnProp("Kenney/Furniture/lampRoundFloor", new Vector3(-half + 1.4f, 0f, 4.6f), 1.5f);
            SpawnProp("Kenney/Furniture/bench", new Vector3(4.6f, 0f, -3.4f), 1.6f);

            // A brazier: the one warm thing in the middle of the deck.
            var fire = Prim(PrimitiveType.Cylinder, "Brazier", _root, new Vector3(0f, 0.34f, 4.3f),
                new Vector3(1.0f, 0.34f, 1.0f), metal * 1.2f, 0.4f);
            _ = fire;
            Prim(PrimitiveType.Sphere, "Embers", _root, new Vector3(0f, 0.62f, 4.3f),
                new Vector3(0.74f, 0.22f, 0.74f), new Color(1f, 0.55f, 0.22f), 1.6f);

            var glow = new GameObject("BrazierLight");
            glow.transform.SetParent(_root, false);
            glow.transform.position = new Vector3(0f, 0.9f, 4.3f);
            var point = glow.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = new Color(1f, 0.62f, 0.30f);
            point.range = 8f;
            point.intensity = 1.6f;
            point.shadows = LightShadows.None;

            SpawnLantern(new Vector3(-half + 0.9f, 0f, half - 0.9f));
            SpawnLantern(new Vector3(half - 0.9f, 0f, half - 0.9f));
            SpawnProp("Runner/Nature/plant_bush", new Vector3(half - 1.2f, 0f, -2.6f), 0.85f);
        }

        private void Lounge(Vector3 at, float turn, Color metal)
        {
            var root = new GameObject("Lounger");
            root.transform.SetParent(_root, false);
            root.transform.position = at;
            root.transform.localRotation = Quaternion.Euler(0f, turn, 0f);

            BoxUnder(root.transform, "Seat", new Vector3(0f, 0.34f, 0f), new Vector3(0.85f, 0.16f, 1.7f),
                metal * 1.5f, 0.5f);
            BoxUnder(root.transform, "Cushion", new Vector3(0f, 0.46f, 0.1f), new Vector3(0.78f, 0.12f, 1.35f),
                new Color(0.86f, 0.82f, 0.74f), 0.55f);
            BoxUnder(root.transform, "Back", new Vector3(0f, 0.72f, -0.78f), new Vector3(0.85f, 0.62f, 0.14f),
                metal * 1.45f, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.34f : 0.34f;
                float z = i < 2 ? -0.7f : 0.7f;
                BoxUnder(root.transform, "Leg" + i, new Vector3(x, 0.13f, z), new Vector3(0.08f, 0.26f, 0.08f),
                    metal, 0.35f);
            }
        }

        private void Planter(Vector3 at, Color metal)
        {
            var root = new GameObject("Planter");
            root.transform.SetParent(_root, false);
            root.transform.position = at;

            BoxUnder(root.transform, "Box", new Vector3(0f, 0.30f, 0f), new Vector3(0.9f, 0.60f, 0.9f),
                metal * 1.35f, 0.45f);
            Prim(PrimitiveType.Sphere, "Topiary", root.transform, new Vector3(0f, 0.86f, 0f),
                Vector3.one * 0.74f, new Color(0.24f, 0.46f, 0.26f), 0.45f);
        }

        /// <summary>
        /// A run of bulbs on a sagging wire. The wire is drawn as short segments between the
        /// bulbs, which is the only way to follow a curve without a line renderer — and the curve
        /// is the point: a straight string of lights reads as a wire, a sagging one reads as a
        /// party.
        /// </summary>
        private void BuildStringLights(Vector3 from, Vector3 to, int bulbs, float sag, Color warm)
        {
            var run = new GameObject("StringLights");
            run.transform.SetParent(_root, false);
            var points = RoomDecor.StringLights(from, to, bulbs, sag);
            var wire = new Color(0.16f, 0.15f, 0.18f);

            for (int i = 0; i < points.Length; i++)
            {
                Prim(PrimitiveType.Sphere, "Bulb" + i, run.transform, points[i] - new Vector3(0f, 0.07f, 0f),
                    Vector3.one * 0.17f, warm, 1.8f);

                if (i == 0) continue;

                var a = points[i - 1];
                var b = points[i];
                var middle = (a + b) * 0.5f;
                var delta = b - a;

                var segment = BoxUnder(run.transform, "Wire" + i, middle, new Vector3(0.3f, 0.3f, 0.3f),
                    wire, 0.3f);
                segment.transform.localScale = new Vector3(0.03f, 0.03f, delta.magnitude);
                segment.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            }

            var light = new GameObject("StringLight");
            light.transform.SetParent(run.transform, false);
            light.transform.position = (from + to) * 0.5f - new Vector3(0f, sag * 0.6f, 0f);
            var point = light.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = warm;
            point.range = 9f;
            point.intensity = 1.1f;
            point.shadows = LightShadows.None;
        }

        /// <summary>A sphere, a cylinder or a cube with the room's material, in one line.</summary>
        private GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position,
            Vector3 scale, Color colour, float emission)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            SetColor(go, colour, emission);
            StripCollider(go);
            return go;
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
            StripCollider(rock);
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
            // One place's decor is another's clutter: the cabin's rug, shelf and pot plants have
            // no business on a lawn, and the garden's flower beds would be inside the walls.
            BuildThemeDecor();
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

            foreach (var collider in instance.GetComponentsInChildren<Collider>())
            {
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }

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

        /// <summary>
        /// Builds the furniture the player has bought *and* placed.
        ///
        /// The default room is deliberately bare — the pet and the two bowls, nothing else —
        /// because a bed, a litter box and a ball are now things you earn rather than things that
        /// are just there. Everything else in this room comes from <see cref="PetInventory"/>.
        /// </summary>
        private void BuildPurchasedProps()
        {
            var placed = PetInventory.Placed(Theme);
            for (int i = 0; i < PetShop.All.Length; i++)
            {
                var item = PetShop.All[i];
                if (item.IsFood || !item.Kind.HasValue) continue;
                if (!placed.ContainsKey(item.Id)) continue;

                // Per-scene placement means this map only ever holds what belongs here; the check
                // stays as a second lock so a bad save cannot put a telescope in the garden.
                if (!item.AllowedIn(Theme)) continue;

                BuildProp(item);
            }
        }

        /// <summary>Builds one placed shop prop at the position the player chose for it.</summary>
        private Interactable BuildProp(ShopItem item)
        {
            Vector2 at = PetInventory.PositionOf(item.Id, Theme);
            switch (item.Kind.Value)
            {
                case InteractableKind.Bed: return BuildBed(at);
                case InteractableKind.Ball: return BuildBall(at);
                case InteractableKind.Brush: return BuildBrush(at);
                case InteractableKind.Toilet: return BuildToilet(at);
                case InteractableKind.Bath: return BuildBath(at);
                case InteractableKind.Toy: return BuildToy(at);
                case InteractableKind.AppleTree: return BuildAppleTree(at);
                case InteractableKind.Pond: return BuildPond(at);
                case InteractableKind.GrassHeap: return BuildGrassHeap(at);
                case InteractableKind.Swing: return BuildSwing(at);
                case InteractableKind.Telescope: return BuildTelescope(at);
                case InteractableKind.RockingChair: return BuildRockingChair(at);
                default: return null;
            }
        }

        private Interactable[] BuildBowls()
        {
            Vector2 foodAt = PetInventory.PositionOf(PetShop.FoodBowl, Theme);
            Vector2 waterAt = PetInventory.PositionOf(PetShop.WaterBowl, Theme);

            var food = Bowl("FoodBowl", new Vector3(foodAt.x, 0f, foodAt.y), new Color(0.92f, 0.42f, 0.35f));
            food.Kind = InteractableKind.Food;
            food.Label = "食物碗";
            food.ItemId = PetShop.FoodBowl;
            food.ApproachPoint = new Vector3(foodAt.x, 0f, foodAt.y - 1.2f);
            AddFruit(food.transform, "Runner/Items/apple", new Vector3(0f, 0.18f, 0f), 0.26f);

            var water = Bowl("WaterBowl", new Vector3(waterAt.x, 0f, waterAt.y), new Color(0.35f, 0.62f, 0.85f));
            water.Kind = InteractableKind.Water;
            water.Label = "水碗";
            water.ItemId = PetShop.WaterBowl;
            water.ApproachPoint = new Vector3(waterAt.x, 0f, waterAt.y - 1.2f);
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
            foreach (var collider in fruit.GetComponentsInChildren<Collider>())
            {
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }
        }

        private Interactable BuildBed(Vector2 at)
        {
            var go = new GameObject("Bed");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

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
            interactable.ItemId = "bed";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y + 1.5f);
            return interactable;
        }

        private Interactable BuildBall(Vector2 at)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

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
            interactable.ItemId = "ball";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y - 1.2f);

            // Throwing and fetching need the ball to be a stateful object, not a static prop.
            var ball = go.AddComponent<PetBall>();
            ball.SnapToRest(go.transform.position);
            return interactable;
        }

        private Interactable BuildBrush(Vector2 at)
        {
            var go = new GameObject("Brush");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

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
            interactable.ItemId = "brush";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y - 1.2f);
            return interactable;
        }

        /// <summary>
        /// The litter tray. Deliberately in a corner: a pet that has to cross the room to
        /// reach it is a pet that sometimes does not make it, which is where the mess system
        /// comes from.
        /// </summary>
        private Interactable BuildToilet(Vector2 at)
        {
            var go = new GameObject("Toilet");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

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
            interactable.ItemId = "litter_box";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y + 1.4f);
            return interactable;
        }

        /// <summary>A wash basin with a raised lip, plus a soap bar so it reads as a bath.</summary>
        private Interactable BuildBath(Vector2 at)
        {
            var go = new GameObject("Bath");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

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
            interactable.ItemId = "bath";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y - 1.2f);
            return interactable;
        }

        /// <summary>A small squeaky toy: a yarn ball on the floor, for the pet to pounce on.</summary>
        private Interactable BuildToy(Vector2 at)
        {
            var go = new GameObject("Toy");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Yarn";
            ball.transform.SetParent(go.transform, false);
            ball.transform.localScale = Vector3.one * 0.46f;
            ball.transform.localPosition = new Vector3(0f, 0.23f, 0f);
            SetColor(ball, new Color(0.88f, 0.36f, 0.44f), 0.7f);

            var ear = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ear.name = "Ear";
            ear.transform.SetParent(go.transform, false);
            ear.transform.localScale = Vector3.one * 0.20f;
            ear.transform.localPosition = new Vector3(0.30f, 0.42f, 0f);
            SetColor(ear, new Color(0.98f, 0.90f, 0.85f), 0.7f);

            var hit = go.AddComponent<SphereCollider>();
            hit.radius = 0.42f;
            hit.center = new Vector3(0f, 0.28f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Toy;
            interactable.Label = "玩具（点它让宠物去玩）";
            interactable.ItemId = "toy";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y + 1.2f);
            return interactable;
        }

        /// <summary>An apple tree: trunk, crown, and a <see cref="PetAppleTree"/> that bears fruit.</summary>
        private Interactable BuildAppleTree(Vector2 at)
        {
            var go = new GameObject("AppleTree");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(go.transform, false);
            trunk.transform.localScale = new Vector3(0.34f, 1.2f, 0.34f);
            trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            SetColor(trunk, new Color(0.42f, 0.28f, 0.16f), 0.35f);

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.SetParent(go.transform, false);
            crown.transform.localScale = new Vector3(1.9f, 1.5f, 1.9f);
            crown.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            SetColor(crown, new Color(0.24f, 0.50f, 0.22f), 0.5f);

            for (int i = 0; i < 3; i++)
            {
                float angle = i * 2.1f;
                var apple = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                apple.name = "Apple" + i;
                apple.transform.SetParent(crown.transform, false);
                apple.transform.localScale = Vector3.one * 0.20f;
                apple.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.7f, 0.35f, Mathf.Sin(angle) * 0.7f);
                SetColor(apple, new Color(0.92f, 0.26f, 0.22f), 0.7f);
            }

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(1.2f, 3.2f, 1.2f);
            hit.center = new Vector3(0f, 1.8f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.AppleTree;
            interactable.Label = "苹果树";
            interactable.ItemId = "apple_tree";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y - 1.4f);

            go.AddComponent<PetAppleTree>();
            return interactable;
        }

        /// <summary>A little pond: a shallow stone basin of water, with a <see cref="PetPond"/>.</summary>
        private Interactable BuildPond(Vector2 at)
        {
            var go = new GameObject("Pond");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.SetParent(go.transform, false);
            rim.transform.localScale = new Vector3(2.4f, 0.18f, 1.7f);
            rim.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            SetColor(rim, new Color(0.62f, 0.62f, 0.66f), 0.3f);

            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Water";
            water.transform.SetParent(go.transform, false);
            water.transform.localScale = new Vector3(2.1f, 0.12f, 1.4f);
            water.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            SetColor(water, new Color(0.34f, 0.62f, 0.86f), 0.85f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(2.4f, 0.5f, 1.7f);
            hit.center = new Vector3(0f, 0.22f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Pond;
            interactable.Label = "小池塘";
            interactable.ItemId = "pond";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y + 1.2f);

            go.AddComponent<PetPond>();
            return interactable;
        }

        /// <summary>A soft heap of straw the pet sleeps in — the garden's own bed.</summary>
        private Interactable BuildGrassHeap(Vector2 at)
        {
            var go = new GameObject("GrassHeap");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

            var heap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            heap.name = "Heap";
            heap.transform.SetParent(go.transform, false);
            heap.transform.localScale = new Vector3(1.7f, 0.7f, 1.4f);
            heap.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            SetColor(heap, new Color(0.82f, 0.72f, 0.40f), 0.5f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(1.7f, 0.7f, 1.4f);
            hit.center = new Vector3(0f, 0.35f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.GrassHeap;
            interactable.Label = "草堆";
            interactable.ItemId = "grass_heap";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y + 1.4f);
            return interactable;
        }

        /// <summary>A swing: two posts, a beam, and a seat that sways.</summary>
        private Interactable BuildSwing(Vector2 at)
        {
            var go = new GameObject("Swing");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

            BoxUnder(go.transform, "PostL", new Vector3(-0.55f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.14f),
                new Color(0.55f, 0.40f, 0.26f), 0.45f);
            BoxUnder(go.transform, "PostR", new Vector3(0.55f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.14f),
                new Color(0.55f, 0.40f, 0.26f), 0.45f);
            BoxUnder(go.transform, "Beam", new Vector3(0f, 2.30f, 0f), new Vector3(1.4f, 0.12f, 0.14f),
                new Color(0.60f, 0.44f, 0.30f), 0.45f);
            BoxUnder(go.transform, "Seat", new Vector3(0f, 0.75f, 0f), new Vector3(0.75f, 0.10f, 0.5f),
                new Color(0.72f, 0.52f, 0.30f), 0.5f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(1.4f, 2.4f, 0.8f);
            hit.center = new Vector3(0f, 1.2f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Swing;
            interactable.Label = "秋千";
            interactable.ItemId = "swing";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y - 1.3f);
            return interactable;
        }

        /// <summary>A brass telescope on a tripod, pointing at the night sky.</summary>
        private Interactable BuildTelescope(Vector2 at)
        {
            var go = new GameObject("Telescope");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

            BoxUnder(go.transform, "Leg", new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 1.1f, 0.5f),
                new Color(0.30f, 0.28f, 0.30f), 0.3f);
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "Tube";
            tube.transform.SetParent(go.transform, false);
            tube.transform.localScale = new Vector3(0.22f, 0.9f, 0.22f);
            tube.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            tube.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            SetColor(tube, new Color(0.78f, 0.62f, 0.30f), 0.55f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(0.8f, 2.4f, 0.8f);
            hit.center = new Vector3(0f, 1.2f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.Telescope;
            interactable.Label = "天文望远镜";
            interactable.ItemId = "telescope";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y - 1.3f);
            return interactable;
        }

        /// <summary>A rocking chair on curved runners, for the terrace.</summary>
        private Interactable BuildRockingChair(Vector2 at)
        {
            var go = new GameObject("RockingChair");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(at.x, 0f, at.y);

            BoxUnder(go.transform, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.10f, 0.7f),
                new Color(0.52f, 0.34f, 0.22f), 0.45f);
            BoxUnder(go.transform, "Back", new Vector3(0f, 0.95f, -0.32f), new Vector3(0.8f, 0.95f, 0.10f),
                new Color(0.52f, 0.34f, 0.22f), 0.45f);
            BoxUnder(go.transform, "Runner", new Vector3(0f, 0.18f, 0f), new Vector3(0.9f, 0.10f, 0.16f),
                new Color(0.40f, 0.26f, 0.16f), 0.4f);

            var hit = go.AddComponent<BoxCollider>();
            hit.size = new Vector3(0.9f, 1.2f, 0.8f);
            hit.center = new Vector3(0f, 0.6f, 0f);

            var interactable = go.AddComponent<Interactable>();
            interactable.Kind = InteractableKind.RockingChair;
            interactable.Label = "摇椅";
            interactable.ItemId = "rocking_chair";
            interactable.ApproachPoint = new Vector3(at.x, 0f, at.y + 1.4f);
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

        /// <summary>
        /// Gives a surface its own generated texture (lawn, deck, fence cutout, skyline).
        ///
        /// Standard for the ground so it takes the place's light — a lawn lit by the garden's
        /// ambient is most of what makes it read as outdoors — and its own material per call,
        /// because each of these has a different tiling.
        /// </summary>
        private static void SetTextured(GameObject go, Color colour, Texture2D texture, Vector2 tiling)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null || texture == null) return;

            var shader = Shader.Find("Standard");
            if (shader == null) return;

            // Cached by texture + tiling + tint: the room is rebuilt every time the pet moves
            // house, and a fresh material per rebuild would leak one per move.
            string key = texture.GetInstanceID() + "_" +
                         tiling.x.ToString("F2") + "x" + tiling.y.ToString("F2") + "_" +
                         ColorUtility.ToHtmlStringRGB(colour);

            if (!TextureCache.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(shader)
                {
                    name = "Room_tex_" + go.name,
                    mainTexture = texture,
                    color = colour
                };
                material.mainTextureScale = tiling;
                material.SetFloat("_Glossiness", 0.10f);
                TextureCache[key] = material;
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static readonly Dictionary<string, Material> TextureCache = new Dictionary<string, Material>();
    }
}
