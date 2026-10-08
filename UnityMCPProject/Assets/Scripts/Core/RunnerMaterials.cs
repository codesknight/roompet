using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// Materials for the procedurally built level. Loaded from Resources so they also
    /// work in a player build; falls back to a runtime material if an asset is missing.
    /// </summary>
    public static class RunnerMaterials
    {
        private static Material _track;
        private static Material _edge;
        private static Material _obstacle;
        private static Material _barrier;
        private static Material _coin;
        private static Material _powerUp;
        private static Material _path;
        private static Material _ground;
        private static bool _fallbackLogged;

        public static Material Track => _track ??= Load("Runner/Track", new Color(0.055f, 0.065f, 0.145f));
        public static Material Edge => _edge ??= Load("Runner/Edge", new Color(0.25f, 0.85f, 1.0f));
        public static Material Obstacle => _obstacle ??= Load("Runner/Obstacle", new Color(1.0f, 0.18f, 0.45f));
        public static Material Barrier => _barrier ??= Load("Runner/Barrier", new Color(1.0f, 0.62f, 0.15f));
        public static Material Coin => _coin ??= Load("Runner/Coin", new Color(1.0f, 0.85f, 0.25f));
        public static Material PowerUp => _powerUp ??= Load("Runner/PowerUp", new Color(0.35f, 1.0f, 0.55f));

        /// <summary>Dirt running surface, matching the Kenney nature kit's palette.</summary>
        public static Material Path => _path ??= Load("Runner/Path", new Color(0.89f, 0.51f, 0.34f));

        /// <summary>Grass verges either side of the path.</summary>
        public static Material Ground => _ground ??= Load("Runner/Ground", new Color(0.17f, 0.85f, 0.72f));

        private static Material Load(string resourcePath, Color fallbackColor)
        {
            var mat = Resources.Load<Material>(resourcePath);
            if (mat != null) return mat;

            if (!_fallbackLogged)
            {
                _fallbackLogged = true;
                Debug.LogWarning($"[DshRunner] Material 'Resources/{resourcePath}' not found; " +
                                 "using a runtime fallback material.");
            }

            var shader = Shader.Find("DSH/Neon");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader) { name = "Fallback_" + resourcePath.Replace('/', '_') };
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", fallbackColor);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fallbackColor);
            return mat;
        }
    }
}
