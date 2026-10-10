using System.Collections.Generic;
using UnityEngine;

namespace DshMobile
{
    /// <summary>The animals a mini-game character can be.</summary>
    public enum MiniAnimalKind { Cat = 0, Dog = 1, Rabbit = 2, Bear = 3, Fox = 4, Panda = 5, Pig = 6, Frog = 7 }

    /// <summary>
    /// A small animal built out of primitives, for the mini-games' player character.
    ///
    /// Why this lives in <c>DshMobile</c> and not with the pets: the mini-games are a separate
    /// assembly that only references the shared mobile layer, deliberately — a mini-game must not
    /// be able to reach into the pet simulation. But the *character* in them should be the pet, not
    /// a bottle: 跳一跳 shipped with a white cylinder-and-sphere figure, and the report back was the
    /// obvious one — "换成小动物本身".
    ///
    /// Which animal is decided by the species id the pet saved (<c>dshpet.species</c>), read as a
    /// plain PlayerPrefs string. That is the same trick the mini-games already use to get home
    /// (<c>dshpet.away</c>): a shared key is a weaker dependency than a shared assembly reference,
    /// and it cannot accidentally couple the two systems together.
    ///
    /// Built from Sphere/Capsule/Cylinder/Cone primitives, with no colliders and no shadows: the
    /// thing is decorative, and the games it appears in do their own collision in pure arithmetic.
    /// </summary>
    public static class MiniAnimal
    {
        /// <summary>How many animals there are.</summary>
        public static int KindCount => 8;

        /// <summary>
        /// The pet's species id, as an animal.
        ///
        /// The four the pet game actually ships (fox/cat/rabbit/bear) are matched by id; anything
        /// else — including an empty save — becomes a cat, because a hero with no species is still
        /// better than a hero made of cylinders.
        /// </summary>
        public static MiniAnimalKind FromSpeciesId(string speciesId)
        {
            switch ((speciesId ?? "").Trim().ToLowerInvariant())
            {
                case "dog": return MiniAnimalKind.Dog;
                case "rabbit": return MiniAnimalKind.Rabbit;
                case "bear": return MiniAnimalKind.Bear;
                case "fox": return MiniAnimalKind.Fox;
                case "panda": return MiniAnimalKind.Panda;
                case "pig": return MiniAnimalKind.Pig;
                case "frog": return MiniAnimalKind.Frog;
                case "cat": return MiniAnimalKind.Cat;
                default: return MiniAnimalKind.Cat;
            }
        }

        /// <summary>The key the pet game saves its current species under.</summary>
        public const string SpeciesKey = "dshpet.species";

        /// <summary>The animal on this device, straight from the save.</summary>
        public static MiniAnimalKind Current => FromSpeciesId(PlayerPrefs.GetString(SpeciesKey, "cat"));

        public static string Name(MiniAnimalKind kind)
        {
            switch (kind)
            {
                case MiniAnimalKind.Cat: return "猫";
                case MiniAnimalKind.Dog: return "狗";
                case MiniAnimalKind.Rabbit: return "兔";
                case MiniAnimalKind.Bear: return "熊";
                case MiniAnimalKind.Fox: return "狐狸";
                case MiniAnimalKind.Panda: return "熊猫";
                case MiniAnimalKind.Pig: return "猪";
                default: return "青蛙";
            }
        }

        /// <summary>The fur colour, matched to the pet of the same species.</summary>
        public static Color Fur(MiniAnimalKind kind)
        {
            switch (kind)
            {
                case MiniAnimalKind.Cat: return new Color(0.95f, 0.72f, 0.42f);
                case MiniAnimalKind.Dog: return new Color(0.80f, 0.62f, 0.42f);
                case MiniAnimalKind.Rabbit: return new Color(0.96f, 0.93f, 0.92f);
                case MiniAnimalKind.Bear: return new Color(0.62f, 0.44f, 0.30f);
                case MiniAnimalKind.Fox: return new Color(0.93f, 0.55f, 0.28f);
                case MiniAnimalKind.Panda: return new Color(0.96f, 0.96f, 0.97f);
                case MiniAnimalKind.Pig: return new Color(0.96f, 0.70f, 0.76f);
                default: return new Color(0.55f, 0.82f, 0.45f);
            }
        }

        private static string ModelNameFor(MiniAnimalKind kind)
        {
            switch (kind)
            {
                case MiniAnimalKind.Cat: return "animal-cat";
                case MiniAnimalKind.Dog: return "animal-dog";
                case MiniAnimalKind.Rabbit: return "animal-bunny";
                case MiniAnimalKind.Bear: return "animal-polar";
                case MiniAnimalKind.Fox: return "animal-fox";
                case MiniAnimalKind.Panda: return "animal-panda";
                case MiniAnimalKind.Pig: return "animal-pig";
                default: return null;   // Frog: no Kenney model, keep the primitives
            }
        }

        private static readonly Color Dark = new Color(0.16f, 0.13f, 0.16f);
        private static readonly Color White = new Color(0.98f, 0.98f, 0.99f);
        private static readonly Color Pink = new Color(0.93f, 0.62f, 0.68f);

        /// <summary>
        /// Builds one animal under <paramref name="parent"/>, <paramref name="height"/> units tall,
        /// standing on the parent's origin and facing <b>-Z</b> (the way the mini-game cameras look
        /// at it). Returns the root, so a game can tilt or bob the whole thing.
        /// </summary>
        public static Transform Build(Transform parent, MiniAnimalKind kind, float height = 1f)
        {
            var root = new GameObject("MiniAnimal_" + Name(kind)).transform;
            if (parent != null) root.SetParent(parent, false);

            // Prefer the imported Kenney Cube Pets model; fall back to the primitive body when the
            // species has no matching model (frog) or the resource is missing.
            string model = ModelNameFor(kind);
            if (!string.IsNullOrEmpty(model))
            {
                var loaded = KenneyModel.Load(model, root, height);
                if (loaded != null) return root;
            }

            float s = Mathf.Max(0.05f, height);
            Color fur = Fur(kind);
            Color furDark = fur * 0.78f;
            furDark.a = 1f;

            // ---- behind the body: tail, then the ears behind the head ----
            Tail(root, kind, fur, furDark, s);
            Ears(root, kind, fur, furDark, s);

            // ---- body and head ----
            Part(root, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.30f, 0.01f),
                new Vector3(0.40f, 0.35f, 0.44f) * s, fur);
            Part(root, "Belly", PrimitiveType.Sphere, new Vector3(0f, 0.26f, -0.12f),
                new Vector3(0.26f, 0.22f, 0.22f) * s, Lighten(fur, 0.35f));
            Part(root, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.70f, -0.02f),
                new Vector3(0.36f, 0.34f, 0.36f) * s, fur);

            // ---- four little feet, so it reads as standing rather than floating ----
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.14f;
                float z = (i < 2 ? -1f : 1f) * 0.11f;
                Part(root, "Foot" + i, PrimitiveType.Sphere, new Vector3(x, 0.05f, z),
                    new Vector3(0.13f, 0.10f, 0.15f) * s, i < 2 ? fur : furDark);
            }

            // ---- face ----
            Face(root, kind, fur, furDark, s);
            return root;
        }

        private static void Ears(Transform root, MiniAnimalKind kind, Color fur, Color furDark, float s)
        {
            switch (kind)
            {
                case MiniAnimalKind.Rabbit:
                    Tilted(root, "EarL", PrimitiveType.Capsule, new Vector3(-0.09f, 0.94f, 0.02f),
                        new Vector3(0.10f, 0.20f, 0.10f) * s, fur, new Vector3(0f, 0f, 14f));
                    Tilted(root, "EarR", PrimitiveType.Capsule, new Vector3(0.09f, 0.94f, 0.02f),
                        new Vector3(0.10f, 0.20f, 0.10f) * s, fur, new Vector3(0f, 0f, -14f));
                    Tilted(root, "EarInner", PrimitiveType.Capsule, new Vector3(-0.09f, 0.94f, -0.02f),
                        new Vector3(0.05f, 0.15f, 0.05f) * s, Pink, new Vector3(0f, 0f, 14f));
                    break;

                case MiniAnimalKind.Fox:
                    // Diamond-rotated rather than merely tilted: a tilted cube reads as a slab, and a
                    // cube turned 45° about the view axis reads as a triangle, which is the whole
                    // shape of a fox's ear.
                    Tilted(root, "EarL", PrimitiveType.Cube, new Vector3(-0.13f, 0.92f, 0f),
                        new Vector3(0.15f, 0.15f, 0.05f) * s, furDark, new Vector3(0f, 0f, 32f));
                    Tilted(root, "EarR", PrimitiveType.Cube, new Vector3(0.13f, 0.92f, 0f),
                        new Vector3(0.15f, 0.15f, 0.05f) * s, furDark, new Vector3(0f, 0f, -32f));
                    break;

                case MiniAnimalKind.Bear:
                case MiniAnimalKind.Panda:
                    Color outer = kind == MiniAnimalKind.Panda ? Dark : furDark;
                    Part(root, "EarL", PrimitiveType.Sphere, new Vector3(-0.15f, 0.88f, 0f),
                        Vector3.one * 0.14f * s, outer);
                    Part(root, "EarR", PrimitiveType.Sphere, new Vector3(0.15f, 0.88f, 0f),
                        Vector3.one * 0.14f * s, outer);
                    break;

                case MiniAnimalKind.Dog:
                    Tilted(root, "EarL", PrimitiveType.Capsule, new Vector3(-0.18f, 0.72f, 0f),
                        new Vector3(0.10f, 0.14f, 0.10f) * s, furDark, new Vector3(0f, 0f, -24f));
                    Tilted(root, "EarR", PrimitiveType.Capsule, new Vector3(0.18f, 0.72f, 0f),
                        new Vector3(0.10f, 0.14f, 0.10f) * s, furDark, new Vector3(0f, 0f, 24f));
                    break;

                case MiniAnimalKind.Pig:
                    Tilted(root, "EarL", PrimitiveType.Cube, new Vector3(-0.14f, 0.88f, 0f),
                        new Vector3(0.11f, 0.12f, 0.05f) * s, furDark, new Vector3(0f, 0f, -26f));
                    Tilted(root, "EarR", PrimitiveType.Cube, new Vector3(0.14f, 0.88f, 0f),
                        new Vector3(0.11f, 0.12f, 0.05f) * s, furDark, new Vector3(0f, 0f, 26f));
                    break;

                case MiniAnimalKind.Frog:
                    // A frog's eyes are on top of its head, which is the whole silhouette.
                    Part(root, "EyeBulgeL", PrimitiveType.Sphere, new Vector3(-0.12f, 0.88f, -0.06f),
                        Vector3.one * 0.15f * s, fur);
                    Part(root, "EyeBulgeR", PrimitiveType.Sphere, new Vector3(0.12f, 0.88f, -0.06f),
                        Vector3.one * 0.15f * s, fur);
                    Part(root, "PupilL", PrimitiveType.Sphere, new Vector3(-0.12f, 0.90f, -0.12f),
                        Vector3.one * 0.07f * s, Dark);
                    Part(root, "PupilR", PrimitiveType.Sphere, new Vector3(0.12f, 0.90f, -0.12f),
                        Vector3.one * 0.07f * s, Dark);
                    break;

                default: // Cat: two pointed ears, the one shape everybody reads instantly.
                    Tilted(root, "EarL", PrimitiveType.Cube, new Vector3(-0.12f, 0.92f, 0f),
                        new Vector3(0.14f, 0.14f, 0.05f) * s, fur, new Vector3(0f, 0f, 38f));
                    Tilted(root, "EarR", PrimitiveType.Cube, new Vector3(0.12f, 0.92f, 0f),
                        new Vector3(0.14f, 0.14f, 0.05f) * s, fur, new Vector3(0f, 0f, -38f));
                    break;
            }
        }

        private static void Tail(Transform root, MiniAnimalKind kind, Color fur, Color furDark, float s)
        {
            switch (kind)
            {
                case MiniAnimalKind.Fox:
                    Tilted(root, "Tail", PrimitiveType.Capsule, new Vector3(0f, 0.42f, 0.26f),
                        new Vector3(0.16f, 0.24f, 0.16f) * s, fur, new Vector3(58f, 0f, 0f));
                    Part(root, "TailTip", PrimitiveType.Sphere, new Vector3(0f, 0.58f, 0.40f),
                        Vector3.one * 0.17f * s, White);
                    break;

                case MiniAnimalKind.Pig:
                    Part(root, "Curl", PrimitiveType.Sphere, new Vector3(0f, 0.40f, 0.24f),
                        Vector3.one * 0.10f * s, Pink);
                    break;

                case MiniAnimalKind.Rabbit:
                    Part(root, "Tail", PrimitiveType.Sphere, new Vector3(0f, 0.28f, 0.26f),
                        Vector3.one * 0.16f * s, White);
                    break;

                default:
                    Tilted(root, "Tail", PrimitiveType.Capsule, new Vector3(0f, 0.38f, 0.24f),
                        new Vector3(0.10f, 0.18f, 0.10f) * s, furDark, new Vector3(52f, 0f, 0f));
                    break;
            }
        }

        private static void Face(Transform root, MiniAnimalKind kind, Color fur, Color furDark, float s)
        {
            if (kind == MiniAnimalKind.Frog)
            {
                // A wide mouth instead of a muzzle — and no nose, because frogs do not have one
                // sticking out of their face.
                Part(root, "Mouth", PrimitiveType.Cube, new Vector3(0f, 0.64f, -0.16f),
                    new Vector3(0.24f, 0.03f, 0.06f) * s, Dark);
                return;
            }

            if (kind == MiniAnimalKind.Pig)
            {
                Part(root, "Snout", PrimitiveType.Sphere, new Vector3(0f, 0.62f, -0.16f),
                    new Vector3(0.20f, 0.14f, 0.12f) * s, Pink);
                Part(root, "NostrilL", PrimitiveType.Sphere, new Vector3(-0.05f, 0.63f, -0.21f),
                    Vector3.one * 0.04f * s, Dark);
                Part(root, "NostrilR", PrimitiveType.Sphere, new Vector3(0.05f, 0.63f, -0.21f),
                    Vector3.one * 0.04f * s, Dark);
            }
            else
            {
                Part(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.63f, -0.14f),
                    new Vector3(0.20f, 0.13f, 0.14f) * s, Lighten(fur, 0.55f));
                Part(root, "Nose", PrimitiveType.Sphere, new Vector3(0f, 0.66f, -0.21f),
                    Vector3.one * 0.06f * s, Dark);
            }

            if (kind == MiniAnimalKind.Panda)
            {
                // The dark patches go on before the eyes, or the eyes vanish inside them.
                Part(root, "PatchL", PrimitiveType.Sphere, new Vector3(-0.09f, 0.75f, -0.13f),
                    new Vector3(0.11f, 0.12f, 0.08f) * s, Dark);
                Part(root, "PatchR", PrimitiveType.Sphere, new Vector3(0.09f, 0.75f, -0.13f),
                    new Vector3(0.11f, 0.12f, 0.08f) * s, Dark);
            }

            Part(root, "EyeL", PrimitiveType.Sphere, new Vector3(-0.09f, 0.76f, -0.16f),
                Vector3.one * 0.07f * s, Dark);
            Part(root, "EyeR", PrimitiveType.Sphere, new Vector3(0.09f, 0.76f, -0.16f),
                Vector3.one * 0.07f * s, Dark);
            Part(root, "GlintL", PrimitiveType.Sphere, new Vector3(-0.10f, 0.78f, -0.19f),
                Vector3.one * 0.03f * s, White);
            Part(root, "GlintR", PrimitiveType.Sphere, new Vector3(0.08f, 0.78f, -0.19f),
                Vector3.one * 0.03f * s, White);

            if (kind == MiniAnimalKind.Cat || kind == MiniAnimalKind.Fox)
            {
                // Whiskers: two thin bars a side, which is all a 3D cat needs at this size.
                for (int i = 0; i < 2; i++)
                {
                    float y = 0.64f + i * 0.04f;
                    Part(root, "WhiskerL" + i, PrimitiveType.Cube, new Vector3(-0.17f, y, -0.14f),
                        new Vector3(0.16f, 0.012f, 0.012f) * s, furDark);
                    Part(root, "WhiskerR" + i, PrimitiveType.Cube, new Vector3(0.17f, y, -0.14f),
                        new Vector3(0.16f, 0.012f, 0.012f) * s, furDark);
                }
            }

            if (kind == MiniAnimalKind.Bear || kind == MiniAnimalKind.Dog || kind == MiniAnimalKind.Pig)
            {
                Part(root, "CheekL", PrimitiveType.Sphere, new Vector3(-0.14f, 0.70f, -0.12f),
                    new Vector3(0.08f, 0.05f, 0.04f) * s, Pink);
                Part(root, "CheekR", PrimitiveType.Sphere, new Vector3(0.14f, 0.70f, -0.12f),
                    new Vector3(0.08f, 0.05f, 0.04f) * s, Pink);
            }
        }

        private static Color Lighten(Color color, float amount)
            => Color.Lerp(color, Color.white, Mathf.Clamp01(amount));

        private static Transform Part(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            // Colliders and shadows are both wasted on a decorative character: the mini-games do
            // their collision in arithmetic (that is the whole reason they are testable), and a
            // shadow-casting hero made of twenty spheres is twenty extra draw calls for nothing.
            //
            // Destroyed *immediately* in the editor and deferred at runtime. `Destroy` alone is
            // deferred everywhere, which means the primitive's collider is still in the physics
            // scene for the rest of the frame it was created in — invisible in a game, and a
            // failing assertion in a test that checks the thing it says it checks.
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            Tint(go, color);
            return go.transform;
        }

        private static Transform Tilted(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Color color, Vector3 rotation)
        {
            var part = Part(parent, name, type, position, scale, color);
            part.localRotation = Quaternion.Euler(rotation);
            return part;
        }

        /// <summary>
        /// Materials are shared per colour, unlike the boxes in the hop game: a character is built
        /// once and never changes colour, so one material per colour is twenty renderers' worth of
        /// batching instead of twenty materials.
        /// </summary>
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();

        private static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            int key = ((int)(color.r * 255f) << 16) | ((int)(color.g * 255f) << 8) | (int)(color.b * 255f);
            Material material;
            if (!Materials.TryGetValue(key, out material) || material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.color = color;
                material.SetFloat("_Glossiness", 0.25f);
                Materials[key] = material;
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
