using System.Collections.Generic;
using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// The fruit, vegetables and bombs the games throw around, built out of primitives.
    ///
    /// One implementation for two games: 接果子 drops them and 切水果 slices them, and a project
    /// where the apple looks different in each game is a project that will fix one and forget the
    /// other. The shapes are deliberately chunky — a stem, a leaf, a calyx — because at the size
    /// these are drawn on a phone the *silhouette* is the only thing that reads.
    /// </summary>
    public static class FruitArt
    {
        /// <summary>Materials are shared per colour: a fruit is a few primitives and never changes.</summary>
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();

        // ------------------------------------------------------------------ colours

        public static readonly Color AppleRed = new Color(0.90f, 0.24f, 0.24f);
        public static readonly Color OrangeSkin = new Color(0.98f, 0.62f, 0.16f);
        public static readonly Color PearGreen = new Color(0.74f, 0.82f, 0.32f);
        public static readonly Color BananaYellow = new Color(0.98f, 0.86f, 0.30f);
        public static readonly Color CarrotOrange = new Color(0.96f, 0.50f, 0.16f);
        public static readonly Color TomatoRed = new Color(0.94f, 0.30f, 0.22f);
        public static readonly Color LeafGreen = new Color(0.36f, 0.66f, 0.30f);
        public static readonly Color StemBrown = new Color(0.44f, 0.30f, 0.18f);
        public static readonly Color BombDark = new Color(0.13f, 0.13f, 0.16f);

        /// <summary>The colour that stands for this fruit, for trails and result text.</summary>
        public static Color Skin(FruitKind kind)
        {
            switch (kind)
            {
                case FruitKind.Apple: return AppleRed;
                case FruitKind.Orange: return OrangeSkin;
                case FruitKind.Pear: return PearGreen;
                case FruitKind.Banana: return BananaYellow;
                case FruitKind.Carrot: return CarrotOrange;
                default: return TomatoRed;
            }
        }

        // ------------------------------------------------------------------ fruit

        /// <summary>
        /// One fruit or vegetable, out of primitives, sized so its widest part is
        /// <paramref name="half"/> world units from the centre.
        /// </summary>
        public static void Build(Transform root, FruitKind kind, float half)
        {
            switch (kind)
            {
                case FruitKind.Apple:
                    Ball(root, "Apple", new Vector3(0f, 0f, 0f), new Vector3(1f, 0.94f, 1f) * half * 2f, AppleRed);
                    Stick(root, "Stem", new Vector3(0f, half * 1.05f, 0f), new Vector3(0.06f, half * 0.5f, 0.06f), StemBrown, 12f);
                    Ball(root, "Leaf", new Vector3(half * 0.42f, half * 1.12f, 0f),
                        new Vector3(half * 0.9f, half * 0.16f, half * 0.5f), LeafGreen);
                    break;

                case FruitKind.Orange:
                    Ball(root, "Orange", Vector3.zero, Vector3.one * half * 2f, OrangeSkin);
                    Ball(root, "Navel", new Vector3(0f, -half * 0.92f, 0f),
                        new Vector3(half * 0.5f, half * 0.2f, half * 0.5f), new Color(0.86f, 0.48f, 0.10f));
                    Ball(root, "Leaf", new Vector3(half * 0.34f, half * 0.98f, 0f),
                        new Vector3(half * 0.7f, half * 0.14f, half * 0.4f), LeafGreen);
                    break;

                case FruitKind.Pear:
                    // A pear is two spheres: a small one sitting in a big one.
                    Ball(root, "PearBody", new Vector3(0f, -half * 0.28f, 0f),
                        new Vector3(half * 1.7f, half * 1.5f, half * 1.7f), PearGreen);
                    Ball(root, "PearTop", new Vector3(0f, half * 0.62f, 0f),
                        new Vector3(half * 1.1f, half * 1.1f, half * 1.1f), PearGreen);
                    Stick(root, "Stem", new Vector3(0f, half * 1.3f, 0f), new Vector3(0.05f, half * 0.4f, 0.05f), StemBrown, 8f);
                    break;

                case FruitKind.Banana:
                    // A banana is an arc of four overlapping spheres, which at this size is
                    // indistinguishable from a curved mesh and costs nothing to build.
                    for (int i = 0; i < 4; i++)
                    {
                        float t = i / 3f;
                        float bx = Mathf.Lerp(-half * 0.75f, half * 0.75f, t);
                        float by = Mathf.Sin(t * Mathf.PI) * half * 0.45f;
                        Ball(root, "Banana" + i, new Vector3(bx, by, 0f),
                            new Vector3(half * 0.9f, half * 0.8f, half * 0.8f),
                            Color.Lerp(BananaYellow, new Color(0.86f, 0.70f, 0.22f), t));
                    }
                    Ball(root, "Tip", new Vector3(half * 0.95f, half * 0.1f, 0f),
                        Vector3.one * half * 0.34f, StemBrown);
                    break;

                case FruitKind.Carrot:
                    // A cone pointing down: the one shape in this list that says "vegetable".
                    var body = Primitive(root, "Carrot", PrimitiveType.Cylinder, Vector3.zero,
                        new Vector3(half * 0.95f, half * 1.5f, half * 0.95f), CarrotOrange);
                    body.localRotation = Quaternion.Euler(180f, 0f, 0f);
                    Ball(root, "Tip", new Vector3(0f, -half * 1.5f, 0f), Vector3.one * half * 0.4f, CarrotOrange);
                    for (int i = 0; i < 3; i++)
                    {
                        var leaf = Primitive(root, "Leaf" + i, PrimitiveType.Cube,
                            new Vector3(0f, half * 1.75f, 0f),
                            new Vector3(half * 0.22f, half * 1.1f, half * 0.22f), LeafGreen);
                        leaf.localRotation = Quaternion.Euler(0f, 0f, -28f + i * 28f);
                    }
                    break;

                default: // Tomato: a squashed red ball with a green star on top.
                    Ball(root, "Tomato", Vector3.zero, new Vector3(half * 2f, half * 1.7f, half * 2f), TomatoRed);
                    for (int i = 0; i < 5; i++)
                    {
                        var calyx = Primitive(root, "Calyx" + i, PrimitiveType.Cube,
                            new Vector3(0f, half * 0.9f, 0f),
                            new Vector3(half * 0.5f, half * 0.08f, half * 0.2f), LeafGreen);
                        calyx.localRotation = Quaternion.Euler(0f, i * 72f, 0f);
                    }
                    break;
            }
        }

        /// <summary>
        /// One half of a sliced fruit, spinning away.
        ///
        /// A half is the fruit squashed along the cut and pushed to one side, with a pale "cut face"
        /// — building a real hemisphere out of primitives is not possible, and at this size the
        /// readable part is "a piece of the fruit, flying off, with the inside showing".
        /// </summary>
        public static void BuildHalf(Transform root, FruitKind kind, float half, int side)
        {
            float dir = side < 0 ? -1f : 1f;
            var tilt = Quaternion.Euler(0f, 0f, dir * 24f);
            root.localRotation = tilt;

            var piece = Primitive(root, "Half", PrimitiveType.Sphere,
                new Vector3(dir * half * 0.30f, 0f, 0f),
                new Vector3(half * 1.7f, half * 1.5f, half * 1.1f), Skin(kind));
            piece.localRotation = Quaternion.Euler(0f, 0f, dir * 18f);

            // The cut face: a flat disc in the fruit's own flesh colour, so the slice reads as
            // "cut open" rather than "a smaller fruit".
            Primitive(root, "Flesh", PrimitiveType.Cylinder,
                new Vector3(dir * half * 0.34f, 0f, -half * 0.34f),
                new Vector3(half * 1.5f, half * 0.06f, half * 0.9f),
                Color.Lerp(Skin(kind), Color.white, 0.55f));

            root.localRotation = Quaternion.identity;
        }

        /// <summary>A bomb: the one thing you must not slice.</summary>
        public static void BuildBomb(Transform root, float half)
        {
            Ball(root, "Bomb", Vector3.zero, Vector3.one * half * 2f, BombDark);
            Ball(root, "Highlight", new Vector3(-half * 0.35f, half * 0.35f, -half * 0.7f),
                Vector3.one * half * 0.5f, new Color(0.42f, 0.42f, 0.48f));
            Stick(root, "Fuse", new Vector3(0f, half * 1.15f, 0f), new Vector3(0.07f, half * 0.4f, 0.07f),
                new Color(0.72f, 0.62f, 0.42f), 14f);
            Ball(root, "Spark", new Vector3(-half * 0.16f, half * 1.5f, 0f), Vector3.one * half * 0.34f,
                new Color(1f, 0.72f, 0.24f));
        }

        // ------------------------------------------------------------------ plumbing

        private static void Ball(Transform root, string name, Vector3 position, Vector3 scale, Color color)
            => Primitive(root, name, PrimitiveType.Sphere, position, scale, color);

        private static void Stick(Transform root, string name, Vector3 position, Vector3 scale, Color color,
            float tilt)
        {
            var stick = Primitive(root, name, PrimitiveType.Cylinder, position, scale, color);
            stick.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private static Transform Primitive(Transform root, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = MaterialFor(color);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                // Immediate in the editor (tests count these) and deferred at runtime, the same
                // rule the mini animals follow.
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            return go.transform;
        }

        private static Material MaterialFor(Color color)
        {
            int key = ((int)(color.r * 255f) << 16) | ((int)(color.g * 255f) << 8) | (int)(color.b * 255f);
            Material material;
            if (Materials.TryGetValue(key, out material) && material != null) return material;

            material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.45f);
            Materials[key] = material;
            return material;
        }
    }
}
