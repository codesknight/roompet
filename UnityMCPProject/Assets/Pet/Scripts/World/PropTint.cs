using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Paints a prop with its own colour, durably.
    ///
    /// The Kenney nature props share the runner's material assets, so they arrive in the pet
    /// room wearing the runner's palette (its teal grass), and any later change to the runner's
    /// theme would repaint this room with it.
    ///
    /// A MaterialPropertyBlock is the right tool — it overrides the colour per renderer without
    /// writing to the shared asset — but a property block is NOT serialized, so applying one
    /// from a build-time helper works only until the scene is saved and reloaded, which is
    /// exactly the case here: the room is authored into the scene. Living on a component makes
    /// the colour serialized data that re-applies itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class PropTint : MonoBehaviour
    {
        public Color Tint = Color.white;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake() => Apply();

        /// <summary>Keeps the editor preview honest while tuning the colour.</summary>
        private void OnValidate() => Apply();

        public void Apply()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, Tint);
                block.SetColor(BaseColorId, Tint);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// Natural colour for a prop, from its name. Null when the prop should keep its own.
        /// </summary>
        public static Color? ForName(string objectName)
        {
            string name = (objectName ?? "").ToLowerInvariant();
            if (name.Contains("bush") || name.Contains("tree") || name.Contains("grass") || name.Contains("leaf"))
                return new Color(0.34f, 0.62f, 0.32f);
            if (name.Contains("flower"))
                return new Color(0.95f, 0.82f, 0.36f);
            if (name.Contains("mushroom"))
                return new Color(0.85f, 0.35f, 0.32f);
            if (name.Contains("rock") || name.Contains("stone"))
                return new Color(0.62f, 0.62f, 0.66f);
            return null;
        }

        /// <summary>
        /// Warm-darken factor for the Kenney furniture, whose near-white parts (<c>carpetWhite</c>,
        /// <c>metalLight</c>) read as glowing under the room's light. A multiply — not a replace —
        /// keeps every part's own colour while toning the whites down.
        /// </summary>
        public static readonly Color FurnitureFactor = new Color(0.86f, 0.84f, 0.80f);

        /// <summary>
        /// Multiplies every material's colour by <paramref name="factor"/>, creating per-instance
        /// material copies so the shared FBX materials are left untouched. The nature props use the
        /// SET tint in <see cref="ForName"/>; the furniture uses this multiply.
        /// </summary>
        public static void MultiplyColors(GameObject instance, Color factor)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var src = materials[i];
                    if (src == null || !src.HasProperty("_Color")) continue;

                    Color c = src.GetColor("_Color");
                    var copy = new Material(src);
                    copy.SetColor("_Color", new Color(c.r * factor.r, c.g * factor.g, c.b * factor.b, c.a));
                    materials[i] = copy;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }
    }
}
