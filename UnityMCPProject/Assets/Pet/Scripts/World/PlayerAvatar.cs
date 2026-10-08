using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The player's character: a small cube-built figure, same construction rules as the pet
    /// (every mesh hangs off a unit-scale bone, so nothing inherits a squashed parent).
    ///
    /// It exists so the room can be walked around rather than clicked at, which is the
    /// foundation for throwing the ball, fetching, and the door to other activities.
    /// </summary>
    public class PlayerAvatar : MonoBehaviour
    {
        [Header("Palette")]
        public Color Jacket = new Color(0.26f, 0.62f, 0.72f);
        public Color Trousers = new Color(0.24f, 0.28f, 0.42f);
        public Color Skin = new Color(0.94f, 0.78f, 0.64f);
        public Color Hair = new Color(0.22f, 0.16f, 0.14f);
        public Color Shoe = new Color(0.30f, 0.22f, 0.20f);

        [Header("Proportions")]
        public float LegLength = 0.42f;
        public float BodyHeight = 0.52f;
        public float BodyWidth = 0.46f;
        public float BodyDepth = 0.26f;
        public float HeadSize = 0.30f;
        public float ArmLength = 0.42f;

        [Header("Animation")]
        public float StepCyclePerMetre = 3.2f;
        public float LegSwingDegrees = 34f;
        public float ArmSwingDegrees = 26f;
        public float BobAmplitude = 0.035f;

        private Transform _rig;
        private Transform _body;
        private Transform _head;
        private readonly List<Transform> _hips = new List<Transform>();
        private readonly List<Transform> _shoulders = new List<Transform>();
        private Transform _shadow;

        private Vector3 _rigBase;
        private float _phase;
        private float _locomotion;
        private float _bob;
        private float _idle;
        private bool _built;
        private bool _editorMode;

        /// <summary>Total standing height, used by the camera framing.</summary>
        public float Height => LegLength + BodyHeight + HeadSize;

        private void Awake() => Build(false);

        public void Build(bool editorMode)
        {
            _editorMode = editorMode;
            if (_built) return;

            Clear();
            Construct();
            _built = true;
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (_editorMode || !Application.isPlaying) DestroyImmediate(child);
                else Destroy(child);
            }
            _hips.Clear();
            _shoulders.Clear();
            _rig = null;
            _body = null;
            _head = null;
            _shadow = null;
            _built = false;
        }

        private void Construct()
        {
            _rig = Bone("Rig", transform, Vector3.zero);
            // Idle sway swings both ways; bias the rest pose up so the feet never dip
            // through the floor.
            _rigBase = _rig.localPosition + new Vector3(0f, 0.008f, 0f);

            float bodyY = LegLength + BodyHeight * 0.5f;

            _body = Mesh("Torso", Jacket, _rig, new Vector3(0f, bodyY, 0f),
                new Vector3(BodyWidth, BodyHeight, BodyDepth));

            // A slightly darker chest panel reads as clothing rather than a plain block.
            Mesh("Collar", Trousers, _rig, new Vector3(0f, bodyY + BodyHeight * 0.34f, BodyDepth * 0.52f),
                new Vector3(BodyWidth * 0.72f, BodyHeight * 0.22f, 0.06f));

            float headY = LegLength + BodyHeight + HeadSize * 0.5f;
            _head = Bone("HeadBone", _rig, new Vector3(0f, headY, 0f));
            Mesh("Head", Skin, _head, Vector3.zero, Vector3.one * HeadSize);
            Mesh("Hair", Hair, _head, new Vector3(0f, HeadSize * 0.34f, -HeadSize * 0.04f),
                new Vector3(HeadSize * 1.06f, HeadSize * 0.40f, HeadSize * 1.04f));
            for (int side = -1; side <= 1; side += 2)
            {
                Mesh("Eye", Hair, _head,
                    new Vector3(side * HeadSize * 0.22f, HeadSize * 0.05f, HeadSize * 0.5f),
                    new Vector3(HeadSize * 0.14f, HeadSize * 0.16f, HeadSize * 0.08f));
            }

            // Legs.
            float hipX = BodyWidth * 0.24f;
            AddLimb("LegLBone", "LegL", new Vector3(-hipX, LegLength, 0f), LegLength, Trousers, Shoe);
            AddLimb("LegRBone", "LegR", new Vector3(hipX, LegLength, 0f), LegLength, Trousers, Shoe);

            // Arms.
            float shoulderX = BodyWidth * 0.5f + 0.06f;
            float shoulderY = LegLength + BodyHeight * 0.86f;
            AddLimb("ArmLBone", "ArmL", new Vector3(-shoulderX, shoulderY, 0f), ArmLength, Jacket, Skin);
            AddLimb("ArmRBone", "ArmR", new Vector3(shoulderX, shoulderY, 0f), ArmLength, Jacket, Skin);

            _shadow = Mesh("Shadow", null, transform, new Vector3(0f, 0.012f, 0f),
                new Vector3(0.85f, 0.02f, 0.85f));
            var shadowRenderer = _shadow.GetComponent<Renderer>();
            if (shadowRenderer != null) shadowRenderer.sharedMaterial = ShadowMaterial();
        }

        private void AddLimb(string boneName, string meshName, Vector3 at, float length,
            Color upperColor, Color lowerColor)
        {
            var bone = Bone(boneName, _rig, at);
            float thickness = length * 0.24f;

            Mesh(meshName, upperColor, bone, new Vector3(0f, -length * 0.45f, 0f),
                new Vector3(thickness, length * 0.9f, thickness));
            Mesh(meshName + "End", lowerColor, bone, new Vector3(0f, -length * 0.95f, thickness * 0.2f),
                new Vector3(thickness * 1.2f, thickness * 0.8f, thickness * 1.6f));

            if (boneName.StartsWith("Leg")) _hips.Add(bone);
            else _shoulders.Add(bone);
        }

        private Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            bone.localRotation = Quaternion.identity;
            bone.localScale = Vector3.one;
            return bone;
        }

        private Transform Mesh(string name, Color? color, Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (_editorMode || !Application.isPlaying) DestroyImmediate(collider);
                else Destroy(collider);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            if (color.HasValue)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = CachedMaterial(color.Value);
            }
            return go.transform;
        }

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        private static Material CachedMaterial(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGB(color);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var shader = Shader.Find("DSH/Neon") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Player_" + key };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 0.92f);
            if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", 0.22f);
            if (material.HasProperty("_RimPower")) material.SetFloat("_RimPower", 2.6f);
            Cache[key] = material;
            return material;
        }

        private static Material ShadowMaterial()
        {
            if (Cache.TryGetValue("shadow", out var cached) && cached != null) return cached;
            var shader = Shader.Find("DSH/Neon") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Player_Shadow" };
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.12f, 0.11f, 0.16f));
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 0.35f);
            if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", 0f);
            Cache["shadow"] = material;
            return material;
        }

        // ---------------------------------------------------------------- animation

        public void SetLocomotion(float amount) => _locomotion = Mathf.Clamp01(amount);

        public void FaceTowards(Vector3 worldPoint, float smoothing = 10f)
        {
            Vector3 flat = worldPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0004f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(flat.normalized, Vector3.up),
                1f - Mathf.Exp(-smoothing * Time.deltaTime));
        }

        private void Update()
        {
            if (!_built || _rig == null) return;

            float dt = Time.deltaTime;
            _phase += dt * _locomotion * StepCyclePerMetre;
            _idle += dt;

            float swing = Mathf.Sin(_phase) * LegSwingDegrees * _locomotion;
            float opposite = Mathf.Sin(_phase + Mathf.PI) * LegSwingDegrees * _locomotion;

            SetRotation(_hips, 0, swing);
            SetRotation(_hips, 1, opposite);

            // Arms counter-swing against the legs, which is what makes a walk read as a walk.
            SetRotation(_shoulders, 0, -opposite * (ArmSwingDegrees / LegSwingDegrees));
            SetRotation(_shoulders, 1, -swing * (ArmSwingDegrees / LegSwingDegrees));

            float bobTarget = Mathf.Abs(Mathf.Sin(_phase)) * BobAmplitude * _locomotion;
            _bob = Mathf.Lerp(_bob, bobTarget, 1f - Mathf.Exp(-14f * dt));

            // A slow breathing sway keeps the figure alive while standing still.
            float idleSway = Mathf.Sin(_idle * 1.4f) * 0.006f * (1f - _locomotion);
            _rig.localPosition = _rigBase + new Vector3(0f, _bob + idleSway, 0f);

            if (_body != null)
            {
                _body.localRotation = Quaternion.Euler(0f, Mathf.Sin(_phase) * 3f * _locomotion, 0f);
            }
            if (_head != null)
            {
                _head.localRotation = Quaternion.Euler(Mathf.Sin(_idle * 1.1f) * 2f, 0f, 0f);
            }
        }

        private static void SetRotation(List<Transform> list, int index, float degrees)
        {
            if (index < 0 || index >= list.Count) return;
            var bone = list[index];
            if (bone == null) return;
            bone.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
