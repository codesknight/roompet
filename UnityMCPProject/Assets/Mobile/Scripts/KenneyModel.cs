using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Loads the shared Kenney Cube Pets models (CC0, imported in the pet-body round) for any
    /// scene that wants a nicer character than primitives. The mini-games live in their own
    /// assembly that cannot reference the pet code, so this loader sits in the shared mobile
    /// layer next to <see cref="MiniAnimal"/>.
    /// </summary>
    public static class KenneyModel
    {
        /// <summary>
        /// Instantiates a model from Assets/Resources/Kenney/Pets/, scaled so its height equals
        /// <paramref name="height"/>, centred on the parent's origin on X/Z and mounted on the
        /// floor (feet at y=0) — or centred vertically when <paramref name="centerVertically"/> is
        /// set, for characters whose pivot is their mid-height. Returns null when the resource is
        /// missing so the caller can fall back.
        /// </summary>
        public static Transform Load(string resourceName, Transform parent, float height, bool centerVertically = false)
        {
            var prefab = Resources.Load<GameObject>("Kenney/Pets/" + resourceName);
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, parent);
            instance.name = "BodyModel_" + resourceName;

            float target = Mathf.Max(0.05f, height);
            var raw = MeasureBounds(instance);
            instance.transform.localScale = Vector3.one * (target / Mathf.Max(0.01f, raw.size.y));

            // Bounds are world-space; convert to the parent's local space before using them as an
            // offset, or a character standing anywhere but the origin would be pushed off to that
            // spot. X/Z centring keeps the games' "root origin" contract.
            var placed = MeasureBounds(instance);
            Vector3 center = parent.InverseTransformPoint(placed.center);
            Vector3 min = parent.InverseTransformPoint(placed.min);
            float y = centerVertically ? -center.y : -min.y;
            instance.transform.localPosition = new Vector3(-center.x, y, -center.z);
            instance.transform.localRotation = Quaternion.identity;

            StripColliders(instance);
            return instance.transform;
        }

        private static Bounds MeasureBounds(GameObject go)
        {
            bool any = false;
            var result = new Bounds(go.transform.position, Vector3.zero);
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var matrix = filter.transform.localToWorldMatrix;
                var scale = matrix.lossyScale;
                var centre = matrix.MultiplyPoint3x4(mesh.bounds.center);
                var extents = new Vector3(
                    Mathf.Abs(mesh.bounds.extents.x * scale.x),
                    Mathf.Abs(mesh.bounds.extents.y * scale.y),
                    Mathf.Abs(mesh.bounds.extents.z * scale.z));
                var world = new Bounds(centre, extents * 2f);
                if (!any) { result = world; any = true; }
                else result.Encapsulate(world);
            }
            if (!any) return new Bounds(go.transform.position, Vector3.one);
            return result;
        }

        private static void StripColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
        }
    }
}
