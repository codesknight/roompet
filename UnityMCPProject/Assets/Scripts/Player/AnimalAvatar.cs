using System.Collections.Generic;
using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// Builds the player character out of primitives and animates it procedurally, so the
    /// runner needs no rigged model. The silhouette is deliberately cube-shaped: the
    /// original player was a cube, the track is boxy, and the whole scene is low-poly.
    ///
    /// Structure matters here. Every mesh cube is parented to a bone with unit scale —
    /// parenting a cube to another *scaled* cube makes it inherit that non-uniform scale,
    /// which silently collapses the whole character into the torso (the bug that the
    /// bounds check in the test plan caught).
    ///
    ///   Rig (unit scale)
    ///     Body, Chest            <- leaf meshes
    ///     Head (bone) -> mesh, snout, nose, eyes, ears
    ///     Tail (bone) -> seg2 (bone) -> seg3 (bone)
    ///     HipFL/FR/BL/BR (bones) -> leg mesh, paw mesh
    /// </summary>
    public class AnimalAvatar : MonoBehaviour
    {
        [Header("Proportions (metres)")]
        public float BodyWidth = 0.52f;
        public float BodyHeight = 0.40f;
        public float BodyLength = 0.72f;
        public float HeadSize = 0.36f;
        public float LegLength = 0.30f;
        public float LegThickness = 0.12f;

        [Header("Animation")]
        [Tooltip("Radians of leg swing per metre travelled.")]
        public float LegCyclePerMetre = 1.5f;
        public float LegSwingDegrees = 42f;
        public float BodyBobAmount = 0.035f;
        public float TailSwayDegrees = 16f;
        public float EarFlapDegrees = 18f;

        private Transform _rig;
        private Transform _head;
        private Transform _tail;
        private Transform _tailMid;
        private Transform _tailTip;
        private Transform _earLeft;
        private Transform _earRight;

        private readonly List<Transform> _hips = new List<Transform>();
        private readonly List<Transform> _legs = new List<Transform>();

        private float _phase;
        private float _bob;
        private float _lastBob;
        private float _tailSway;
        private float _earFlap;
        private bool _built;
        private bool _editorMode;

        private static Material _fur;
        private static Material _belly;
        private static Material _dark;

        /// <summary>Feet sit this far below the player's centre, so the base of the rig
        /// lines up with the bottom of the collision box.</summary>
        private const float FootY = -0.5f;

        private void Awake() => Build(false);

        /// <summary>Creates the parts. Safe to call again; the old parts are removed first.</summary>
        public void Build(bool editorMode)
        {
            _editorMode = editorMode;
            LoadMaterials();

            // The scene can already carry a built avatar (it is previewed in the editor and
            // saved with the scene). Rebuild it only when it is missing, otherwise every
            // play-mode Awake would stack a second copy of the whole character on top.
            if (TryBindExisting())
            {
                _built = true;
                return;
            }

            Clear();
            Construct();
            _built = true;
        }

        private void LoadMaterials()
        {
            _fur = LoadMaterial("Runner/AnimalFur", new Color(0.93f, 0.45f, 0.22f));
            _belly = LoadMaterial("Runner/AnimalBelly", new Color(0.97f, 0.87f, 0.74f));
            _dark = LoadMaterial("Runner/AnimalDark", new Color(0.26f, 0.16f, 0.14f));
        }

        /// <summary>Re-caches the bones of an avatar that already exists in the hierarchy.</summary>
        private bool TryBindExisting()
        {
            var rig = transform.Find("Rig");
            if (rig == null) return false;

            var head = rig.Find("HeadBone");
            if (head == null) return false;

            _rig = rig;
            _head = head;
            _tail = rig.Find("TailBone");
            _tailMid = _tail != null ? _tail.Find("TailMidBone") : null;
            _earLeft = head.Find("EarL");
            _earRight = head.Find("EarR");

            _hips.Clear();
            _legs.Clear();
            string[] hipNames = { "LegFLBone", "LegFRBone", "LegBLBone", "LegBRBone" };
            foreach (var hipName in hipNames)
            {
                var hip = rig.Find(hipName);
                if (hip == null) return false;
                _hips.Add(hip);
            }

            return true;
        }

        private void Construct()
        {
            // Prefer the imported Kenney fox over the primitive cube fox. The whole model is the
            // "rig", so the PlayerController bob/tilt still applies while the per-part animation
            // (legs/tail/ears) no-ops because those bones stay null.
            var model = DshMobile.KenneyModel.Load("animal-fox", transform, 1f, centerVertically: true);
            if (model != null)
            {
                _rig = model;
                _head = _tail = _tailMid = _tailTip = _earLeft = _earRight = null;
                _hips.Clear();
                _legs.Clear();
                return;
            }

            _rig = Bone("Rig", transform, Vector3.zero);

            // ---- torso ----
            Mesh("Body", _fur, _rig, new Vector3(0f, 0f, 0f), new Vector3(BodyWidth, BodyHeight, BodyLength));
            Mesh("Chest", _belly, _rig, new Vector3(0f, -0.09f, BodyLength * 0.42f),
                new Vector3(BodyWidth * 0.60f, BodyHeight * 0.48f, 0.13f));

            // ---- head ----
            _head = Bone("HeadBone", _rig, new Vector3(0f, 0.30f, BodyLength * 0.30f));
            Mesh("Head", _fur, _head, Vector3.zero, new Vector3(HeadSize, HeadSize * 0.95f, HeadSize));
            Mesh("Snout", _belly, _head, new Vector3(0f, -HeadSize * 0.16f, HeadSize * 0.62f),
                new Vector3(HeadSize * 0.46f, HeadSize * 0.34f, HeadSize * 0.40f));
            Mesh("Nose", _dark, _head, new Vector3(0f, -HeadSize * 0.08f, HeadSize * 0.86f),
                new Vector3(HeadSize * 0.20f, HeadSize * 0.15f, HeadSize * 0.13f));

            for (int side = -1; side <= 1; side += 2)
            {
                Mesh("Eye", _dark, _head,
                    new Vector3(side * HeadSize * 0.28f, HeadSize * 0.16f, HeadSize * 0.52f),
                    Vector3.one * HeadSize * 0.17f);
            }

            _earLeft = Mesh("EarL", _fur, _head,
                new Vector3(-HeadSize * 0.30f, HeadSize * 0.72f, -HeadSize * 0.05f),
                new Vector3(HeadSize * 0.22f, HeadSize * 0.52f, HeadSize * 0.14f));
            _earRight = Mesh("EarR", _fur, _head,
                new Vector3(HeadSize * 0.30f, HeadSize * 0.72f, -HeadSize * 0.05f),
                new Vector3(HeadSize * 0.22f, HeadSize * 0.52f, HeadSize * 0.14f));

            // ---- tail: three segments on a bone chain, cream tip ----
            _tail = Bone("TailBone", _rig, new Vector3(0f, 0.10f, -BodyLength * 0.48f));
            _tail.localRotation = Quaternion.Euler(-34f, 0f, 0f);
            Mesh("TailA", _fur, _tail, new Vector3(0f, 0f, -0.08f), new Vector3(0.15f, 0.15f, 0.20f));

            _tailMid = Bone("TailMidBone", _tail, new Vector3(0f, 0f, -0.17f));
            Mesh("TailB", _fur, _tailMid, new Vector3(0f, 0f, -0.08f), new Vector3(0.13f, 0.13f, 0.18f));

            var tailEnd = Bone("TailEndBone", _tailMid, new Vector3(0f, 0f, -0.16f));
            _tailTip = Mesh("TailTip", _belly, tailEnd, new Vector3(0f, 0f, -0.07f),
                new Vector3(0.11f, 0.11f, 0.16f));

            // ---- legs ----
            float hipY = FootY + LegLength;
            float hipX = BodyWidth * 0.32f;
            float frontZ = BodyLength * 0.30f;
            float backZ = -BodyLength * 0.30f;

            AddLeg("LegFL", new Vector3(-hipX, hipY, frontZ));
            AddLeg("LegFR", new Vector3(hipX, hipY, frontZ));
            AddLeg("LegBL", new Vector3(-hipX, hipY, backZ));
            AddLeg("LegBR", new Vector3(hipX, hipY, backZ));
        }

        private void AddLeg(string name, Vector3 hipPosition)
        {
            var hip = Bone(name + "Bone", _rig, hipPosition);
            Mesh(name, _fur, hip, new Vector3(0f, -LegLength * 0.5f, 0f),
                new Vector3(LegThickness, LegLength, LegThickness));
            Mesh(name + "Paw", _dark, hip,
                new Vector3(0f, -LegLength + LegThickness * 0.30f, LegThickness * 0.35f),
                new Vector3(LegThickness * 1.2f, LegThickness * 0.6f, LegThickness * 1.7f));

            _hips.Add(hip);
        }

        /// <summary>An empty transform with unit scale; safe to parent scaled meshes to.</summary>
        private Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            bone.localRotation = Quaternion.identity;
            bone.localScale = Vector3.one;
            return bone;
        }

        private Transform Mesh(string name, Material material, Transform parent, Vector3 localPosition,
            Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) RemovePart(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            return go.transform;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                RemovePart(transform.GetChild(i).gameObject);
            }

            _hips.Clear();
            _legs.Clear();
            _rig = null;
            _head = null;
            _tail = null;
            _tailMid = null;
            _tailTip = null;
            _earLeft = null;
            _earRight = null;
            _built = false;
        }

        private void RemovePart(Object target)
        {
            if (target == null) return;
            // Destroy() is deferred and is a no-op outside play mode, which would leave
            // duplicate parts behind when the avatar is rebuilt in the editor.
            if (_editorMode || !Application.isPlaying) DestroyImmediate(target);
            else Destroy(target);
        }

        private static Material LoadMaterial(string resourcePath, Color fallback)
        {
            var material = Resources.Load<Material>(resourcePath);
            if (material != null) return material;

            var shader = Shader.Find("DSH/Neon") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "Fallback_" + resourcePath };
            if (material.HasProperty("_Color")) material.SetColor("_Color", fallback);
            return material;
        }

        private void Update()
        {
            if (!_built || _rig == null) return;

            var gm = GameManager.Instance;
            bool playing = gm != null && gm.State == GameState.Playing;
            float speed = playing ? gm.CurrentSpeed : 0f;

            float dt = Time.deltaTime;
            _phase += speed * dt * LegCyclePerMetre;

            // Trot: diagonal pairs swing together.
            if (_hips.Count == 4)
            {
                float swing = Mathf.Sin(_phase) * LegSwingDegrees;
                float opposite = Mathf.Sin(_phase + Mathf.PI) * LegSwingDegrees;
                SetLegAngle(0, swing);
                SetLegAngle(1, opposite);
                SetLegAngle(2, opposite);
                SetLegAngle(3, swing);
            }

            // Whole body bobs at twice the stride rate.
            float bobTarget = playing ? Mathf.Sin(_phase * 2f) * BodyBobAmount : 0f;
            _bob = Mathf.Lerp(_bob, bobTarget, 1f - Mathf.Exp(-14f * dt));
            var rigPos = _rig.localPosition;
            _rig.localPosition = new Vector3(rigPos.x, rigPos.y + (_bob - _lastBob), rigPos.z);
            _lastBob = _bob;

            if (_tail != null)
            {
                float sway = Mathf.Sin(_phase * 0.5f + Time.time * 2.2f) * TailSwayDegrees;
                _tailSway = Mathf.Lerp(_tailSway, sway, 1f - Mathf.Exp(-6f * dt));
                _tail.localRotation = Quaternion.Euler(-34f + Mathf.Sin(_phase) * 8f, 0f, _tailSway);
            }
            if (_tailMid != null) _tailMid.localRotation = Quaternion.Euler(0f, 0f, _tailSway * 0.6f);

            // Ears prick up while airborne.
            bool airborne = gm != null && gm.Player != null && !gm.Player.IsGrounded;
            float flapTarget = airborne ? -EarFlapDegrees : 0f;
            _earFlap = Mathf.Lerp(_earFlap, flapTarget, 1f - Mathf.Exp(-10f * dt));
            SetEar(_earLeft, _earFlap, -7f);
            SetEar(_earRight, _earFlap, 7f);

            if (_head != null)
            {
                float nod = playing ? Mathf.Sin(_phase * 2f) * 3f : 0f;
                _head.localRotation = Quaternion.Euler(nod, 0f, 0f);
            }
        }

        private static void SetEar(Transform ear, float pitch, float roll)
        {
            if (ear == null) return;
            ear.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        private void SetLegAngle(int index, float degrees)
        {
            if (index < 0 || index >= _hips.Count) return;
            var hip = _hips[index];
            if (hip == null) return;
            hip.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
