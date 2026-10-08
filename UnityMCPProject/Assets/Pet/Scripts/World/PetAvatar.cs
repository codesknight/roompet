using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The pet's body. Built from primitives from a <see cref="PetSpecies"/> description,
    /// so swapping animals is a data change rather than an asset swap.
    ///
    /// Structure rule (same one the runner's avatar had to learn the hard way): every mesh
    /// cube hangs off a bone with unit scale. Parenting a cube to another *scaled* cube
    /// makes it inherit that non-uniform scale and the whole creature collapses into its
    /// own torso.
    ///
    ///   root
    ///     Shadow                  flat disc, does not bob
    ///     Rig (unit scale)
    ///       Body, Chest
    ///       HeadBone  -> head, snout, nose, eyes, ears
    ///       TailBone  -> segment chain
    ///       LegBones  -> leg + paw
    /// </summary>
    public class PetAvatar : MonoBehaviour
    {
        [Header("Animation")]
        public float LegCyclePerMetre = 2.6f;
        public float LegSwingDegrees = 38f;
        public float BreathAmplitude = 0.012f;
        public float BobAmplitude = 0.03f;
        public float TurnSmoothing = 8f;

        // A private copy, never a reference into PetSpecies.All — Unity deserializes this
        // field in place and would otherwise write scene data into the shared species table.
        private PetSpecies _species = PetSpecies.All[0].Copy();
        private Transform _rig;
        private Transform _body;
        private Transform _head;
        private Transform _tail;
        private Transform _tailMid;
        private Transform _earLeft;
        private Transform _earRight;
        private readonly List<Transform> _hips = new List<Transform>();
        private Transform _shadow;

        private float _legPhase;
        private float _bob;
        private float _breath;
        private float _tailSway;
        private float _locomotion;
        private float _actionWeight;
        private PetAction _action = PetAction.Idle;
        private float _actionTimer;
        private bool _built;
        private bool _editorMode;
        private Vector3 _baseScale = Vector3.one;

        /// <summary>Resting local position of the rig. Idle motion is applied as an offset
        /// from this every frame rather than accumulated onto the current value.</summary>
        private Vector3 _rigBase;

        /// <summary>Which species the current hierarchy was built for. Serialised so a scene
        /// authored in the editor can be reused, but only by a pet of the same species.</summary>
        [SerializeField] private string _builtSpeciesId = "";

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        private void Awake() => Build(_species, false);

        public PetSpecies Species => _species;

        public void Build(PetSpecies species, bool editorMode)
        {
            var next = species != null ? species.Copy() : PetSpecies.All[0].Copy();
            _editorMode = editorMode;
            _species = next;

            // Reuse the existing hierarchy only when it was built for THIS species.
            // Binding unconditionally is what made every swapped pet keep the fox's body.
            if (_builtSpeciesId == next.Id && TryBindExisting())
            {
                _built = true;
                return;
            }

            Clear();
            Construct();
            _builtSpeciesId = next.Id;
            _built = true;
        }

        // ------------------------------------------------------------------ building

        private bool TryBindExisting()
        {
            var rig = transform.Find("Rig");
            if (rig == null) return false;
            var head = rig.Find("HeadBone");
            if (head == null) return false;

            _rig = rig;
            _rigBase = rig.localPosition;
            _body = rig.Find("Body");
            _head = head;
            _tail = rig.Find("TailBone");
            _tailMid = _tail != null ? _tail.Find("TailMidBone") : null;
            _earLeft = head.Find("EarL");
            _earRight = head.Find("EarR");
            _shadow = transform.Find("Shadow");

            _hips.Clear();
            string[] names = { "LegFLBone", "LegFRBone", "LegBLBone", "LegBRBone" };
            foreach (var n in names)
            {
                var hip = rig.Find(n);
                if (hip == null) return false;
                _hips.Add(hip);
            }
            return true;
        }

        private void Construct()
        {
            float s = Mathf.Max(0.5f, _species.BodyScale);
            float length = _species.BodyLength * s;
            float width = length * 1.35f;
            float height = length * 0.95f;
            float legLength = length * 0.85f;
            float headSize = _species.HeadScale * s;

            _rig = Bone("Rig", transform, Vector3.zero);
            // Breath swings upward from the rest pose so the paws never dip through the floor.
            _rigBase = _rig.localPosition + new Vector3(0f, BreathAmplitude, 0f);

            // Feet on the floor: the body floats legLength above y=0.
            float bodyY = legLength + height * 0.5f;

            _body = Mesh("Body", Fur(), _rig, new Vector3(0f, bodyY, 0f), new Vector3(width, height, length));
            Mesh("Chest", Belly(), _rig, new Vector3(0f, bodyY - height * 0.16f, length * 0.44f),
                new Vector3(width * 0.62f, height * 0.62f, length * 0.22f));

            // ---- head ----
            _head = Bone("HeadBone", _rig, new Vector3(0f, bodyY + height * 0.58f, length * 0.34f));
            Mesh("Head", Fur(), _head, Vector3.zero, new Vector3(headSize, headSize * 0.94f, headSize * 0.92f));
            Mesh("Snout", Belly(), _head, new Vector3(0f, -headSize * 0.16f, headSize * 0.60f),
                new Vector3(headSize * 0.44f, headSize * 0.32f, headSize * 0.36f));
            Mesh("Nose", Accent(), _head, new Vector3(0f, -headSize * 0.08f, headSize * 0.82f),
                new Vector3(headSize * 0.20f, headSize * 0.14f, headSize * 0.12f));

            for (int side = -1; side <= 1; side += 2)
            {
                Mesh("Eye", Accent(), _head,
                    new Vector3(side * headSize * 0.27f, headSize * 0.14f, headSize * 0.48f),
                    Vector3.one * headSize * 0.16f);
            }

            BuildEars(headSize);

            // ---- tail ----
            _tail = Bone("TailBone", _rig, new Vector3(0f, bodyY + height * 0.18f, -length * 0.46f));
            BuildTail();

            // ---- legs ----
            float hipX = width * 0.32f;
            float frontZ = length * 0.28f;
            float backZ = -length * 0.28f;
            AddLeg("LegFLBone", "LegFL", new Vector3(-hipX, legLength, frontZ), legLength, width);
            AddLeg("LegFRBone", "LegFR", new Vector3(hipX, legLength, frontZ), legLength, width);
            AddLeg("LegBLBone", "LegBL", new Vector3(-hipX, legLength, backZ), legLength, width);
            AddLeg("LegBRBone", "LegBR", new Vector3(hipX, legLength, backZ), legLength, width);

            // ---- soft contact shadow so the pet does not look like it floats ----
            _shadow = Mesh("Shadow", null, transform, new Vector3(0f, 0.012f, 0f),
                new Vector3(width * 1.9f, 0.02f, length * 2.1f));
            var shadowRenderer = _shadow.GetComponent<Renderer>();
            if (shadowRenderer != null) shadowRenderer.sharedMaterial = ShadowMaterial();

            _baseScale = transform.localScale;
        }

        private void BuildEars(float headSize)
        {
            float earLength = _species.EarLength;
            float thickness = headSize * 0.20f;

            switch (_species.Ears)
            {
                case EarStyle.Long:
                    _earLeft = Mesh("EarL", Fur(), _head, new Vector3(-headSize * 0.24f, headSize * 0.60f, 0f),
                        new Vector3(thickness * 0.85f, earLength, thickness * 0.6f));
                    _earRight = Mesh("EarR", Fur(), _head, new Vector3(headSize * 0.24f, headSize * 0.60f, 0f),
                        new Vector3(thickness * 0.85f, earLength, thickness * 0.6f));
                    break;

                case EarStyle.Round:
                    _earLeft = Mesh("EarL", Fur(), _head, new Vector3(-headSize * 0.36f, headSize * 0.48f, 0f),
                        Vector3.one * earLength);
                    _earRight = Mesh("EarR", Fur(), _head, new Vector3(headSize * 0.36f, headSize * 0.48f, 0f),
                        Vector3.one * earLength);
                    break;

                case EarStyle.Small:
                    _earLeft = Mesh("EarL", Fur(), _head, new Vector3(-headSize * 0.34f, headSize * 0.46f, 0f),
                        new Vector3(earLength, earLength, earLength * 0.6f));
                    _earRight = Mesh("EarR", Fur(), _head, new Vector3(headSize * 0.34f, headSize * 0.46f, 0f),
                        new Vector3(earLength, earLength, earLength * 0.6f));
                    break;

                default: // Pointy
                    _earLeft = Mesh("EarL", Fur(), _head, new Vector3(-headSize * 0.32f, headSize * 0.66f, 0f),
                        new Vector3(thickness, earLength, thickness * 0.7f));
                    _earRight = Mesh("EarR", Fur(), _head, new Vector3(headSize * 0.32f, headSize * 0.66f, 0f),
                        new Vector3(thickness, earLength, thickness * 0.7f));
                    break;
            }
        }

        private void BuildTail()
        {
            float length = _species.TailLength;
            float thickness = length * 0.45f;

            switch (_species.Tail)
            {
                case TailStyle.Puff:
                    Mesh("TailA", Belly(), _tail, new Vector3(0f, 0f, -length * 0.35f),
                        Vector3.one * length * 0.75f);
                    break;

                case TailStyle.Short:
                    Mesh("TailA", Fur(), _tail, new Vector3(0f, 0f, -length * 0.4f),
                        new Vector3(thickness, thickness, length * 0.8f));
                    break;

                case TailStyle.Curly:
                    Mesh("TailA", Fur(), _tail, new Vector3(0f, length * 0.20f, -length * 0.24f),
                        new Vector3(thickness * 0.85f, thickness * 0.85f, length * 0.34f));
                    _tailMid = Bone("TailMidBone", _tail, new Vector3(0f, length * 0.38f, -length * 0.46f));
                    Mesh("TailB", Fur(), _tailMid, new Vector3(0f, length * 0.18f, 0f),
                        new Vector3(thickness * 0.7f, thickness * 0.7f, length * 0.30f));
                    _tailMid.localRotation = Quaternion.Euler(-55f, 0f, 0f);
                    _tail.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                    break;

                default: // Bushy
                    Mesh("TailA", Fur(), _tail, new Vector3(0f, 0f, -length * 0.22f),
                        new Vector3(thickness * 1.25f, thickness * 1.25f, length * 0.40f));
                    _tailMid = Bone("TailMidBone", _tail, new Vector3(0f, 0f, -length * 0.42f));
                    Mesh("TailB", Fur(), _tailMid, new Vector3(0f, 0f, -length * 0.22f),
                        new Vector3(thickness * 1.05f, thickness * 1.05f, length * 0.36f));
                    var end = Bone("TailEndBone", _tailMid, new Vector3(0f, 0f, -length * 0.40f));
                    Mesh("TailTip", Belly(), end, new Vector3(0f, 0f, -length * 0.14f),
                        new Vector3(thickness * 0.75f, thickness * 0.75f, length * 0.26f));
                    _tail.localRotation = Quaternion.Euler(28f, 0f, 0f);
                    break;
            }
        }

        private void AddLeg(string boneName, string legName, Vector3 hipLocal, float legLength, float bodyWidth)
        {
            var hip = Bone(boneName, _rig, hipLocal);
            float thickness = bodyWidth * 0.19f;
            Mesh(legName, Fur(), hip, new Vector3(0f, -legLength * 0.5f, 0f),
                new Vector3(thickness, legLength, thickness));
            Mesh(legName + "Paw", Accent(), hip,
                new Vector3(0f, -legLength + thickness * 0.35f, thickness * 0.4f),
                new Vector3(thickness * 1.2f, thickness * 0.7f, thickness * 1.7f));
            _hips.Add(hip);
        }

        // ------------------------------------------------------------------ plumbing

        private Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            bone.localRotation = Quaternion.identity;
            bone.localScale = Vector3.one;
            return bone;
        }

        private Transform Mesh(string name, Material material, Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            StripCollider(go);

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
            for (int i = transform.childCount - 1; i >= 0; i--) RemovePart(transform.GetChild(i).gameObject);
            _hips.Clear();
            _rig = _body = _head = _tail = _tailMid = _earLeft = _earRight = _shadow = null;
            _built = false;
        }

        private void RemovePart(Object target)
        {
            if (target == null) return;

            // Only ever called for whole GameObjects. Deactivating first matters because
            // Destroy() is deferred, so a species swap would otherwise keep the outgoing
            // body visible (and measurable) for the rest of the frame.
            var go = target as GameObject;
            if (go == null)
            {
                var component = target as Component;
                if (component != null) go = component.gameObject;
            }
            if (go != null) go.SetActive(false);

            if (_editorMode || !Application.isPlaying) DestroyImmediate(target);
            else Destroy(target);
        }

        /// <summary>Strips a component without touching the object's active state — used to
        /// remove the collider a primitive comes with.</summary>
        private void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (_editorMode || !Application.isPlaying) DestroyImmediate(collider);
            else Destroy(collider);
        }

        // ----------------------------------------------------------------- materials

        private Material Fur() => CachedMaterial(_species.Fur, 0.85f);
        private Material Belly() => CachedMaterial(_species.Belly, 0.9f);
        private Material Accent() => CachedMaterial(_species.Accent, 0.6f);

        private static Material CachedMaterial(Color color, float emission)
        {
            string key = ColorUtility.ToHtmlStringRGB(color) + "_" + emission.ToString("F2");
            if (MaterialCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var shader = Shader.Find("DSH/Neon") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Pet_" + key };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", emission);
            if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", 0.28f);
            if (material.HasProperty("_RimPower")) material.SetFloat("_RimPower", 2.4f);
            MaterialCache[key] = material;
            return material;
        }

        private static Material ShadowMaterial()
        {
            if (MaterialCache.TryGetValue("shadow", out var cached) && cached != null) return cached;
            var shader = Shader.Find("DSH/Neon") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Pet_Shadow" };
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.12f, 0.11f, 0.16f));
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 0.35f);
            if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", 0f);
            MaterialCache["shadow"] = material;
            return material;
        }

        // ---------------------------------------------------------------- behaviour

        /// <summary>One-shot action the avatar should play; falls back to Idle when it ends.</summary>
        public void PlayAction(PetAction action, float seconds = 1.6f)
        {
            _action = action;
            _actionTimer = Mathf.Max(0.1f, seconds);
        }

        public PetAction CurrentAction => _actionTimer > 0f ? _action : PetAction.Idle;

        /// <summary>0 = standing still, 1 = full walking speed.</summary>
        public void SetLocomotion(float amount) => _locomotion = Mathf.Clamp01(amount);

        public void FaceTowards(Vector3 worldPoint, float smoothing = -1f)
        {
            Vector3 flat = worldPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0004f) return;
            var wanted = Quaternion.LookRotation(flat.normalized, Vector3.up);
            float rate = smoothing > 0f ? smoothing : TurnSmoothing;
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted,
                1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        private void Update()
        {
            if (!_built || _rig == null) return;

            float dt = Time.deltaTime;
            if (_actionTimer > 0f) _actionTimer -= dt;

            PetAction action = CurrentAction;
            float target = action == PetAction.Idle ? 0f : 1f;
            _actionWeight = Mathf.Lerp(_actionWeight, target, 1f - Mathf.Exp(-8f * dt));

            // Walk cycle scales with locomotion, and the pet keeps its legs moving a little
            // even while standing so it never looks frozen.
            float cycleSpeed = _locomotion * 2.2f + 0.35f;
            _legPhase += dt * cycleSpeed * LegCyclePerMetre;
            AnimateLegs(action);

            _breath += dt * 1.6f;
            float breath = Mathf.Sin(_breath) * BreathAmplitude;
            float bobTarget = Mathf.Sin(_legPhase * 2f) * BobAmplitude * _locomotion;
            _bob = Mathf.Lerp(_bob, bobTarget, 1f - Mathf.Exp(-12f * dt));

            float crouch = 0f;
            float lean = 0f;
            switch (action)
            {
                case PetAction.Sit: crouch = 0.10f; lean = -6f; break;
                case PetAction.Sleep: crouch = 0.24f; lean = -14f; break;
                case PetAction.Sad: crouch = 0.06f; lean = -8f; break;
                case PetAction.Eat: crouch = 0.05f; break;
                case PetAction.Happy: crouch = -0.02f; break;
                case PetAction.Jump: crouch = 0.10f * Mathf.Abs(Mathf.Sin(Time.time * 8f)); break;
            }

            // Absolute placement, not accumulated deltas: the idle breath term used to be
            // summed frame after frame, which floated the whole pet ~1.1 m into the air.
            float rigOffset = breath + _bob - crouch * _actionWeight;
            _rig.localPosition = _rigBase + new Vector3(0f, rigOffset, 0f);

            if (_body != null)
            {
                _body.localRotation = Quaternion.Slerp(_body.localRotation,
                    Quaternion.Euler(0f, 0f, 0f), 1f - Mathf.Exp(-6f * dt));
            }

            AnimateHead(action, lean, dt);
            AnimateTail(action, dt);
            AnimateEars(action, dt);
        }

        private void AnimateLegs(PetAction action)
        {
            if (_hips.Count != 4) return;
            float swing = Mathf.Sin(_legPhase) * LegSwingDegrees * Mathf.Max(_locomotion, 0.12f);
            float opposite = Mathf.Sin(_legPhase + Mathf.PI) * LegSwingDegrees * Mathf.Max(_locomotion, 0.12f);

            if (action == PetAction.Sleep)
            {
                SetLeg(0, 78f); SetLeg(1, 78f); SetLeg(2, 78f); SetLeg(3, 78f);
                return;
            }
            if (action == PetAction.Sit)
            {
                SetLeg(0, 0f); SetLeg(1, 0f); SetLeg(2, 70f); SetLeg(3, 70f);
                return;
            }
            SetLeg(0, swing); SetLeg(1, opposite); SetLeg(2, opposite); SetLeg(3, swing);
        }

        private void SetLeg(int index, float degrees)
        {
            if (index < 0 || index >= _hips.Count) return;
            var hip = _hips[index];
            if (hip == null) return;
            hip.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }

        private void AnimateHead(PetAction action, float lean, float dt)
        {
            if (_head == null) return;

            float pitch = lean;
            float yaw = 0f;
            float roll = 0f;

            switch (action)
            {
                case PetAction.Curious: roll = 24f; pitch = -4f; break;
                case PetAction.Eat: pitch = 34f + Mathf.Sin(Time.time * 9f) * 9f; break;
                case PetAction.Drink: pitch = 26f + Mathf.Sin(Time.time * 7f) * 6f; break;
                case PetAction.Happy: pitch = -8f + Mathf.Sin(Time.time * 6f) * 5f; break;
                case PetAction.Sad: pitch = 20f; break;
                case PetAction.Beg: pitch = -18f; break;
            }

            if (_locomotion > 0.05f) pitch += Mathf.Sin(_legPhase * 2f) * 2.5f;

            _head.localRotation = Quaternion.Slerp(_head.localRotation,
                Quaternion.Euler(pitch, yaw, roll), 1f - Mathf.Exp(-9f * dt));
        }

        private void AnimateTail(PetAction action, float dt)
        {
            if (_tail == null) return;

            float speed = 2.2f;
            float amount = 12f;
            switch (action)
            {
                case PetAction.Wag: speed = 13f; amount = 34f; break;
                case PetAction.Happy: speed = 8f; amount = 22f; break;
                case PetAction.Sad: speed = 1.1f; amount = 5f; break;
                case PetAction.Sleep: speed = 0.8f; amount = 4f; break;
                case PetAction.Play: speed = 9f; amount = 26f; break;
            }

            if (_species.Tail == TailStyle.Short || _species.Tail == TailStyle.Puff) amount *= 0.5f;

            float sway = Mathf.Sin(Time.time * speed) * amount * Mathf.Lerp(0.5f, 1f, _locomotion);
            _tailSway = Mathf.Lerp(_tailSway, sway, 1f - Mathf.Exp(-7f * dt));

            float basePitch = _species.Tail == TailStyle.Curly ? -20f : 28f;
            if (_species.Tail == TailStyle.Bushy) basePitch = 28f;
            if (_species.Tail == TailStyle.Puff) basePitch = 0f;

            _tail.localRotation = Quaternion.Euler(basePitch, 0f, _tailSway);
            if (_tailMid != null) _tailMid.localRotation = Quaternion.Euler(0f, 0f, _tailSway * 0.55f);
        }

        private void AnimateEars(PetAction action, float dt)
        {
            float pitch = 0f;
            float spread = 6f;

            switch (action)
            {
                case PetAction.Sad: pitch = 46f; spread = 16f; break;
                case PetAction.Sleep: pitch = 62f; break;
                case PetAction.Curious: pitch = -8f; spread = 2f; break;
                case PetAction.Happy: pitch = -10f; break;
                case PetAction.Jump: pitch = -16f; break;
                case PetAction.Wag: pitch = -6f; break;
            }

            if (_locomotion > 0.1f) pitch += Mathf.Sin(_legPhase * 2f) * 4f;

            if (_earLeft != null) _earLeft.localRotation = Quaternion.Slerp(_earLeft.localRotation,
                Quaternion.Euler(pitch, 0f, -spread), 1f - Mathf.Exp(-8f * dt));
            if (_earRight != null) _earRight.localRotation = Quaternion.Slerp(_earRight.localRotation,
                Quaternion.Euler(pitch, 0f, spread), 1f - Mathf.Exp(-8f * dt));
        }
    }
}
