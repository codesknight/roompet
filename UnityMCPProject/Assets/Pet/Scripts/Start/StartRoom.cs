using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The porch the game opens on: a wall, a door, a lamp, and the pet waiting beside it.
    ///
    /// Built from primitives like everything else in this project, and deliberately small — the start
    /// screen has one job (be a front door), so the scene is a doorway, a bit of floor and the animal
    /// the player has been looking after. That last part is the one flourish worth having: the first
    /// thing the game says is "your fox is in there", not "welcome to a menu".
    /// </summary>
    public class StartRoom : MonoBehaviour
    {
        public Transform LeftPanel { get; private set; }
        public Transform RightPanel { get; private set; }
        public Transform DoorwayLight { get; private set; }
        public Light Lamp { get; private set; }

        private Transform _pet;
        private Vector3 _petFrom;
        private Vector3 _petTo;

        private readonly List<Material> _ownMaterials = new List<Material>();

        /// <summary>
        /// Frames the doorway for a screen of this shape.
        ///
        /// The doorway is about three metres wide and two and a half tall, and on a portrait phone the
        /// *width* is what constrains the shot — standing four metres in front of it, as the first
        /// version did, put nothing but door on the screen and the menu was written over a piece of
        /// joinery. (The mini-games learned this the hard way; see DEVLOG 坑 64.) So the distance is
        /// solved from whichever of the two dimensions is tighter, and the walk-in is a fraction of that
        /// same distance, which keeps the two shots consistent on any screen.
        /// </summary>
        public static void FrameDoorway(float aspect, float fieldOfView,
            out Vector3 from, out Vector3 to, out Vector3 look)
        {
            look = CameraLook;

            float half = Mathf.Max(0.05f, fieldOfView) * 0.5f * Mathf.Deg2Rad;
            float tan = Mathf.Tan(half);
            float safeAspect = Mathf.Clamp(aspect, 0.3f, 3.5f);

            // Enough room for the frame, the wall either side of it, and the pet standing beside the door.
            // 5.6 rather than 4.6 because at 4.6 the frame's posts touched the screen edges on a portrait
            // phone and the lamp was cropped in half at the left.
            const float wantedWidth = 5.6f;
            const float wantedHeight = 3.1f;

            float byWidth = wantedWidth * 0.5f / (tan * safeAspect);
            float byHeight = wantedHeight * 0.5f / tan;
            float distance = Mathf.Max(4.4f, Mathf.Max(byWidth, byHeight));

            var direction = new Vector3(-0.16f, 0.06f, 1f).normalized;
            from = look - direction * distance;
            to = look - direction * (distance * 0.34f);
        }

        /// <summary>What the camera is looking at: the doorway, a little above the middle of its height.</summary>
        public static readonly Vector3 CameraLook = new Vector3(0f, 1.15f, 0.2f);

        private Vector3 _cameraFrom;
        private Vector3 _cameraTo;

        /// <summary>The aspect the shot was solved for: a rotated phone or a resized window re-solves it.</summary>
        private float _framedAspect;

        private void Awake()
        {
            BuildFloor();
            BuildWall();
            BuildDoor();
            BuildLamp();
            BuildPet();

            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            // 60° rather than a telephoto 46: a narrow lens needs more room, and the doorway is close.
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;

            RefreshFraming();
            camera.transform.position = _cameraFrom;
            camera.transform.rotation = Quaternion.LookRotation(CameraLook - _cameraFrom);

            // The lamp is a pool, not a room light: without a little ambient the floor in front of the
            // camera renders black and the doorway looks like it is floating.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.32f, 0.29f);
        }

        /// <summary>
        /// Re-solves the shot if the screen's shape has changed.
        ///
        /// Necessary rather than tidy: at Awake the camera still reports the *editor's* aspect, not the
        /// Game view's, so the first version framed itself for a landscape window and then played that
        /// framing on a portrait phone — the doorway filled the screen and the menu was written over a
        /// close-up of a door.
        /// </summary>
        public void RefreshFraming()
        {
            var camera = Camera.main;
            if (camera == null) return;
            if (_framedAspect > 0f && Mathf.Approximately(_framedAspect, camera.aspect)) return;

            _framedAspect = camera.aspect;
            FrameDoorway(camera.aspect, camera.fieldOfView, out _cameraFrom, out _cameraTo, out _);
        }

        /// <summary>Moves the camera and the pet along the opening sequence.</summary>
        public void ApplyOpening(float seconds)
        {
            RefreshFraming();
            float push = StartSequence.CameraPush(seconds);

            var camera = Camera.main;
            if (camera != null)
            {
                var position = Vector3.Lerp(_cameraFrom, _cameraTo, push);
                camera.transform.position = position;
                camera.transform.rotation = Quaternion.LookRotation(CameraLook - position);
            }

            float angle = StartSequence.PanelAngle(seconds);
            if (LeftPanel != null) LeftPanel.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (RightPanel != null) RightPanel.localRotation = Quaternion.Euler(0f, -angle, 0f);

            float glow = StartSequence.DoorGlow(seconds);
            if (Lamp != null) Lamp.intensity = 0.55f + glow * 2.6f;
            if (DoorwayLight != null) DoorwayLight.localScale = new Vector3(1.5f, 2.3f, 1f) * glow;

            if (_pet != null) _pet.position = Vector3.Lerp(_petFrom, _petTo, StartSequence.PetWalk(seconds));
        }

        // ------------------------------------------------------------------ building

        private void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(transform, false);

            // Deep enough to run *under* the camera: at 20 m the near edge was visible at the bottom of a
            // portrait screen, so the menu's four buttons were drawn on black instead of on floor.
            floor.transform.localScale = new Vector3(26f, 0.3f, 32f);
            floor.transform.position = new Vector3(0f, -0.15f, 3f);
            Paint(floor, new Color(0.52f, 0.38f, 0.26f));
            Drop(floor);

            // A doormat, because a doorway with nothing in front of it looks like a hole.
            var mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "Mat";
            mat.transform.SetParent(transform, false);
            mat.transform.localScale = new Vector3(1.5f, 0.06f, 0.75f);
            mat.transform.position = new Vector3(0f, 0.03f, -0.85f);
            Paint(mat, new Color(0.62f, 0.30f, 0.30f));
            Drop(mat);
        }

        private void BuildWall()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(transform, false);

            // Deliberately much larger than the doorway. On a portrait phone at 60° the visible height at
            // the wall is about ten metres, so a 4.4 m wall left the top and bottom of the screen black and
            // the shot read as a door standing on a slab in the void rather than as a room.
            wall.transform.localScale = new Vector3(16f, 8f, 0.35f);
            wall.transform.position = new Vector3(0f, 4f, 0.45f);
            Paint(wall, new Color(0.86f, 0.80f, 0.68f));
            Drop(wall);

            // The frame: two posts and a lintel, in a darker wood than the wall.
            var wood = new Color(0.36f, 0.24f, 0.16f);
            Post("FrameL", new Vector3(-1.55f, 1.25f, 0.1f), new Vector3(0.22f, 2.5f, 0.3f), wood);
            Post("FrameR", new Vector3(1.55f, 1.25f, 0.1f), new Vector3(0.22f, 2.5f, 0.3f), wood);
            Post("FrameTop", new Vector3(0f, 2.6f, 0.1f), new Vector3(3.3f, 0.24f, 0.3f), wood);
        }

        private void Post(string name, Vector3 position, Vector3 scale, Color colour)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = name;
            post.transform.SetParent(transform, false);
            post.transform.localScale = scale;
            post.transform.position = position;
            Paint(post, colour);
            Drop(post);
        }

        private void BuildDoor()
        {
            var wood = new Color(0.46f, 0.31f, 0.20f);
            var panel = new Color(0.40f, 0.27f, 0.18f);

            // Two leaves, hinged at the frame: each is a child of an empty pivot so rotating the pivot
            // swings the leaf around its edge rather than around its middle. The left leaf grows to the
            // *right* of its hinge and the right leaf to the left of its, which is what makes them meet
            // in the middle of the doorway — the first version had the sign the other way round and built
            // two doors hanging off the outside of the frame.
            LeftPanel = Hinged("DoorL", new Vector3(-1.44f, 1.25f, 0.1f), wood, panel, 1f);
            RightPanel = Hinged("DoorR", new Vector3(1.44f, 1.25f, 0.1f), wood, panel, -1f);

            // The light that spills out once they are open.
            var light = GameObject.CreatePrimitive(PrimitiveType.Quad);
            light.name = "DoorwayLight";
            light.transform.SetParent(transform, false);
            light.transform.position = new Vector3(0f, 1.25f, 0.55f);
            light.transform.localScale = Vector3.zero;

            var renderer = light.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var material = new Material(shader) { name = "Dsh_DoorwayLight" };
                    material.mainTexture = DshMobile.SoftShadow.Gradient;
                    material.color = new Color(1f, 0.90f, 0.70f, 0.86f);
                    renderer.sharedMaterial = material;
                    _ownMaterials.Add(material);
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Drop(light);
            DoorwayLight = light.transform;
        }

        private Transform Hinged(string name, Vector3 hinge, Color wood, Color inset, float inward)
        {
            var pivot = new GameObject(name + "Pivot").transform;
            pivot.SetParent(transform, false);
            pivot.position = hinge;

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = name;
            leaf.transform.SetParent(pivot, false);
            leaf.transform.localScale = new Vector3(1.44f, 2.5f, 0.16f);
            leaf.transform.localPosition = new Vector3(inward * 0.72f, 0f, 0f);
            Paint(leaf, wood);
            Drop(leaf);

            // Panels on the face of the leaf: the cheapest way to make a cube read as a door.
            for (int row = 0; row < 2; row++)
            {
                var insetPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                insetPanel.name = name + "Panel" + row;
                insetPanel.transform.SetParent(leaf.transform, false);
                insetPanel.transform.localScale = new Vector3(0.62f, 0.34f, 1.06f);
                insetPanel.transform.localPosition = new Vector3(0f, row == 0 ? 0.26f : -0.26f, -0.55f);
                Paint(insetPanel, inset);
                Drop(insetPanel);
            }

            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = name + "Knob";
            knob.transform.SetParent(leaf.transform, false);
            knob.transform.localScale = Vector3.one * 0.09f;

            // On the *free* edge of the leaf — the one that meets the other leaf in the middle. The first
            // version put both knobs on the hinges, which looks like two doors hung backwards.
            knob.transform.localPosition = new Vector3(inward * 0.5f, 0f, -0.58f);
            Paint(knob, new Color(0.92f, 0.82f, 0.45f));
            Drop(knob);

            return pivot;
        }

        private void BuildLamp()
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(-1.75f, 2.9f, -1.35f);

            // A cord from off the top of the frame. Without it the shade reads as a stone floating in the
            // air beside the door — the first capture of this screen looked like a bug.
            var cord = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cord.name = "Cord";
            cord.transform.SetParent(transform, false);
            cord.transform.localScale = new Vector3(0.035f, 8f, 0.035f);
            cord.transform.position = new Vector3(-1.75f, 6.6f, -1.35f);
            Paint(cord, new Color(0.20f, 0.18f, 0.16f));
            Drop(cord);

            var shade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shade.name = "Shade";
            shade.transform.SetParent(go.transform, false);
            shade.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
            Paint(shade, new Color(1f, 0.93f, 0.72f));
            Drop(shade);

            Lamp = go.AddComponent<Light>();
            Lamp.type = LightType.Point;
            Lamp.color = new Color(1f, 0.90f, 0.72f);
            Lamp.intensity = 0.55f;
            Lamp.range = 16f;
            Lamp.shadows = LightShadows.None;

            // A dim fill behind the camera. Not decoration: with only the lamp, everything within a couple
            // of metres of the lens — the mat, the pet's feet, the near floor — lit to nothing at all.
            var fill = new GameObject("Fill");
            fill.transform.SetParent(transform, false);
            fill.transform.position = new Vector3(0.8f, 1.9f, -6.5f);

            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.95f, 0.92f, 0.90f);
            fillLight.intensity = 0.42f;
            fillLight.range = 18f;
            fillLight.shadows = LightShadows.None;
        }

        private void BuildPet()
        {
            var root = new GameObject("Pet").transform;
            root.SetParent(transform, false);

            var kind = DshMobile.MiniAnimal.Current;
            DshMobile.MiniAnimal.Build(root, kind, 0.95f);
            DshMobile.SoftShadow.Attach(root, 0.32f, 0.32f, 0.4f);

            _pet = root;
            _petFrom = new Vector3(1.9f, 0f, -1.15f);
            _petTo = new Vector3(0.35f, 0f, 0.35f);
            root.position = _petFrom;

            // Facing the doorway (MiniAnimal is built facing -Z).
            root.rotation = Quaternion.LookRotation(new Vector3(-0.6f, 0f, 1f));
        }

        private void Paint(GameObject go, Color colour)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var material = new Material(Shader.Find("Standard")) { color = colour };
            material.SetFloat("_Glossiness", 0.22f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _ownMaterials.Add(material);
        }

        private static void Drop(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;

            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
    }
}
