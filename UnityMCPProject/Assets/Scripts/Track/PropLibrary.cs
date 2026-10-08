using System.Collections.Generic;
using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// Loads the CC0 Kenney nature/food models and normalises them on instantiation.
    ///
    /// The models are NOT trusted to import at the size we asked for: the FBX importer
    /// combination Unity ended up with (useFileScale off plus a globalScale that assumed
    /// it was on) leaves them ~10x too large. Rather than depend on importer state,
    /// every instance is measured and scaled to the requested world size here.
    /// </summary>
    public static class PropLibrary
    {
        private class Entry
        {
            public GameObject Prefab;
            public float MaxDimension;
        }

        private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>();

        /// <summary>Root object that remembers which pool an instance belongs to.</summary>
        public class PooledItem : MonoBehaviour
        {
            public string Key;
        }

        private static Entry Get(string resourcePath)
        {
            if (Cache.TryGetValue(resourcePath, out var cached)) return cached;

            var prefab = Resources.Load<GameObject>(resourcePath);
            var entry = new Entry { Prefab = prefab, MaxDimension = 1f };

            if (prefab != null)
            {
                var bounds = Measure(prefab);
                float max = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                entry.MaxDimension = max > 0.0001f ? max : 1f;
            }

            Cache[resourcePath] = entry;
            return entry;
        }

        public static bool Exists(string resourcePath) => Get(resourcePath).Prefab != null;

        /// <summary>Combined renderer bounds of a prefab or instance, in its own space.</summary>
        public static Bounds Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>
        /// Instantiates a prop as a child of <paramref name="parent"/>, scaled so its
        /// largest dimension equals <paramref name="desiredSize"/> metres. The caller is
        /// responsible for sitting it on the ground (see TrackManager.AlignBase).
        /// </summary>
        public static GameObject Spawn(string resourcePath, Transform parent, Vector3 localPosition,
            float desiredSize, float yawDegrees = 0f)
        {
            var entry = Get(resourcePath);
            if (entry.Prefab == null) return null;

            var instance = Object.Instantiate(entry.Prefab, parent);
            instance.name = System.IO.Path.GetFileName(resourcePath);

            float scale = entry.MaxDimension > 0.0001f ? desiredSize / entry.MaxDimension : 1f;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            instance.transform.localPosition = localPosition;

            foreach (var collider in instance.GetComponentsInChildren<Collider>())
            {
                Object.Destroy(collider);
            }

            return instance;
        }

        /// <summary>
        /// The instance's own largest dimension at scale 1, derived from its current
        /// bounds. Used to rescale a pooled instance without reloading the prefab.
        /// </summary>
        public static float BaseSize(GameObject instance)
        {
            var size = Measure(instance).size;
            float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float scale = Mathf.Abs(instance.transform.lossyScale.x);
            if (scale <= 0.0001f) scale = 1f;
            return max / scale;
        }

        /// <summary>Model list helpers so callers do not repeat string literals.</summary>
        public static readonly string[] TallBlockers =
        {
            "Runner/Nature/rock_tallA",
            "Runner/Nature/stone_tallA",
            "Runner/Nature/stump_square",
            "Runner/Nature/rock_largeA"
        };

        public static readonly string[] Fruit =
        {
            "Runner/Items/apple",
            "Runner/Items/carrot",
            "Runner/Items/cherries",
            "Runner/Items/strawberry"
        };

        public static readonly string[] Trees =
        {
            "Runner/Nature/tree_default",
            "Runner/Nature/tree_pineTallA",
            "Runner/Nature/tree_oak",
            "Runner/Nature/tree_simple",
            "Runner/Nature/tree_thin",
            "Runner/Nature/tree_fat",
            "Runner/Nature/tree_blocks",
            "Runner/Nature/tree_pineRoundA"
        };

        public static readonly string[] Undergrowth =
        {
            "Runner/Nature/grass",
            "Runner/Nature/grass_large",
            "Runner/Nature/plant_bush",
            "Runner/Nature/plant_bushLarge",
            "Runner/Nature/flower_redA",
            "Runner/Nature/flower_yellowA",
            "Runner/Nature/flower_purpleA",
            "Runner/Nature/mushroom_red",
            "Runner/Nature/mushroom_tan",
            "Runner/Nature/rock_smallA",
            "Runner/Nature/rock_smallD"
        };

        public static readonly string[] Fences =
        {
            "Runner/Nature/fence_simple",
            "Runner/Nature/fence_planks",
            "Runner/Nature/fence_simpleHigh"
        };
    }
}
