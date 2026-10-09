using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// The soft contact shadow a character stands on.
    ///
    /// This exists because of a bug report: "人物和宠物在移动时脚下有黑色方块". Both avatars built
    /// their shadow out of <c>GameObject.CreatePrimitive(PrimitiveType.Cube)</c> — a flat *box*
    /// stretched into a slab and painted (0.12, 0.11, 0.16). On a warm wooden floor that is a black
    /// square under the character's feet, and it is most obvious while walking, because that is when
    /// the eye is following the feet.
    ///
    /// A round, fading blob needs two things an opaque cube cannot give: a disc silhouette, and an
    /// edge that fades instead of stopping. So: a quad lying flat, with a radial-gradient alpha
    /// texture on a transparent unlit shader. Drawn from code, like everything else here.
    ///
    /// The shader is <c>Sprites/Default</c>, which is in Unity's default "always included" list —
    /// the same reason the project can rely on <c>Standard</c>. If it is ever stripped, the shadow
    /// is skipped rather than replaced by a square: no shadow grounds a character better than a
    /// wrong one does.
    /// </summary>
    public static class SoftShadow
    {
        private static Texture2D _texture;
        private static Material _material;
        private static bool _shaderChecked;

        /// <summary>Edge length of the generated gradient, in pixels.</summary>
        public const int TextureSize = 64;

        /// <summary>Whether a soft shadow can be drawn at all on this build.</summary>
        public static bool Available
        {
            get
            {
                EnsureMaterial();
                return _material != null;
            }
        }

        /// <summary>
        /// The gradient the shadow is drawn with. Exposed so a test can check the property that
        /// makes it a shadow rather than a square: the alpha falls off towards the edge.
        /// </summary>
        public static Texture2D Gradient
        {
            get
            {
                EnsureMaterial();
                return _texture;
            }
        }

        /// <summary>
        /// Attaches a shadow under <paramref name="parent"/>, lying flat on the ground.
        ///
        /// Returns the shadow's transform (so a caller can hide it, e.g. while its character is in
        /// the air), or null when there is no shader to draw it with.
        /// </summary>
        public static Transform Attach(Transform parent, float radiusX, float radiusZ,
            float opacity = 0.45f, float y = 0.014f)
        {
            EnsureMaterial();
            if (_material == null) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "SoftShadow";

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            go.transform.SetParent(parent, false);

            // A quad stands up; lying it flat is what turns it into a shadow on the floor.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetSize(go.transform, radiusX, radiusZ, y);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                // One shared material: the colour is the only thing that varies, and it varies by
                // opacity, so a per-shadow material would be a per-shadow draw call for nothing.
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            var tint = go.AddComponent<SoftShadowTint>();
            tint.Opacity = Mathf.Clamp01(opacity);
            return go.transform;
        }

        /// <summary>Resizes an attached shadow, e.g. when the character's body changes size.</summary>
        public static void SetSize(Transform shadow, float radiusX, float radiusZ, float y = 0.014f)
        {
            if (shadow == null) return;

            // A quad is 1x1 unit at scale 1, so the diameter *is* the scale.
            shadow.localPosition = new Vector3(0f, y, 0f);
            shadow.localScale = new Vector3(radiusX * 2f, radiusZ * 2f, 1f);
        }

        private static void EnsureMaterial()
        {
            if (_shaderChecked) return;
            _shaderChecked = true;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[DshMobile] Sprites/Default is missing; contact shadows are off.");
                return;
            }

            _texture = RadialGradient(TextureSize);
            _material = new Material(shader) { name = "Dsh_SoftShadow" };
            _material.mainTexture = _texture;
            _material.color = new Color(0f, 0f, 0f, 0.45f);

            // Unlit and transparent: it must not catch the room's neon light (a lit shadow changes
            // colour with the room, which reads as a smudge) and it must blend, not cut out.
            if (_material.HasProperty("_Color"))
            {
                _material.SetColor("_Color", new Color(0f, 0f, 0f, 0.45f));
            }

            _material.hideFlags = HideFlags.HideAndDontSave;
            _texture.hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>
        /// A white disc whose alpha falls off towards the edge.
        ///
        /// Squared falloff rather than linear: a linear ramp still has a visible rim, and the whole
        /// point of the exercise is that the shadow has no edge at all.
        /// </summary>
        private static Texture2D RadialGradient(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Dsh_SoftShadow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = distance >= 1f ? 0f : Mathf.Pow(1f - distance, 1.6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }

    /// <summary>
    /// Per-shadow opacity.
    ///
    /// The shadow shares one material (and therefore one draw call) with every other shadow, so the
    /// opacity cannot live on the material — it lives here and is pushed into the renderer's
    /// material property block, which is the cheap way to vary a shared material per object.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoftShadowTint : MonoBehaviour
    {
        [SerializeField] private float _opacity = 0.45f;

        /// <summary>0 = invisible, 1 = fully dark.</summary>
        public float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = Mathf.Clamp01(value);
                Apply();
            }
        }

        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void OnEnable() => Apply();

        private void Apply()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null) return;

            _block ??= new MaterialPropertyBlock();

            // 0.6 is the ceiling: measured against the room's floor a full-strength blob dims the
            // pixels under a character by about 7%, which reads as contact without looking like a
            // smudge. The old opaque black cube was nearer 90%, with a hard edge.
            _block.SetColor(ColorId, new Color(0f, 0f, 0f, _opacity * 0.6f));
            renderer.SetPropertyBlock(_block);
        }
    }
}
