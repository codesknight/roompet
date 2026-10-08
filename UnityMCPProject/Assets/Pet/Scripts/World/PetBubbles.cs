using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The soap bubbles over the bath.
    ///
    /// Self-destroying and self-contained: a bath is a moment, not a state, and a bath that
    /// left anything behind in the scene would be the kind of leak you only notice an hour
    /// later. Bubbles are built from primitives like every other effect in this project, so
    /// there is no particle asset to import and nothing to keep in sync with a prefab.
    /// </summary>
    public class PetBubbles : MonoBehaviour
    {
        public float Lifetime = 1.6f;

        private Transform[] _bubbles;
        private float[] _baseScale;
        private float[] _speed;
        private float[] _phase;
        private float _age;

        private void Start()
        {
            const int count = 7;
            _bubbles = new Transform[count];
            _baseScale = new float[count];
            _speed = new float[count];
            _phase = new float[count];

            var shader = Shader.Find("DSH/Neon");

            for (int i = 0; i < count; i++)
            {
                var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bubble.name = "Bubble" + i;
                bubble.transform.SetParent(transform, false);

                float scale = Random.Range(0.10f, 0.22f);
                _baseScale[i] = scale;
                bubble.transform.localScale = Vector3.one * scale;
                bubble.transform.localPosition = new Vector3(
                    Random.Range(-0.45f, 0.45f), Random.Range(-0.1f, 0.2f), Random.Range(-0.35f, 0.35f));

                // Bubbles must not eat clicks: a collider here would block the bath behind them.
                var collider = bubble.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = bubble.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (shader != null)
                    {
                        var material = new Material(shader);
                        material.color = new Color(0.82f, 0.94f, 1f, 0.75f);
                        renderer.sharedMaterial = material;
                    }
                }

                _bubbles[i] = bubble.transform;
                _speed[i] = Random.Range(0.35f, 0.75f);
                _phase[i] = Random.Range(0f, 6.28f);
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Mathf.Max(0.01f, Lifetime));

            if (_bubbles != null)
            {
                // Pop towards the end rather than fading out: scaling needs no transparent
                // shader, so the effect looks the same on every render pipeline.
                float pop = 1f - Mathf.Pow(t, 3f);

                for (int i = 0; i < _bubbles.Length; i++)
                {
                    var bubble = _bubbles[i];
                    if (bubble == null) continue;

                    var position = bubble.localPosition;
                    position.y += _speed[i] * Time.deltaTime;
                    position.x += Mathf.Sin(_age * 3f + _phase[i]) * 0.12f * Time.deltaTime;
                    bubble.localPosition = position;

                    bubble.localScale = Vector3.one * (_baseScale[i] * pop);
                }
            }

            if (_age >= Lifetime) Destroy(gameObject);
        }
    }
}
