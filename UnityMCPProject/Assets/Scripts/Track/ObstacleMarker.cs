using UnityEngine;

namespace DshRunner
{
    /// <summary>Marks a trigger volume as deadly and records how it must be beaten.</summary>
    public class ObstacleMarker : MonoBehaviour
    {
        public ObstacleKind Kind = ObstacleKind.Block;

        /// <summary>World Y of the top face. A jump is only a clear when the feet are
        /// above this.</summary>
        public float TopY;

        public string FailMessage = "撞上障碍";

        /// <summary>Times this obstacle has been involved in a resolved hit; used by the
        /// level tests and by the small hit/clear flashes.</summary>
        public int HitCount { get; private set; }
        public int ClearedCount { get; private set; }

        private Renderer _renderer;
        private bool _flashing;
        private float _flashTimer;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        public void Configure(ObstacleKind kind, float topY, string failMessage)
        {
            Kind = kind;
            TopY = topY;
            FailMessage = failMessage;
        }

        public void NotifyHit()
        {
            HitCount++;
            Flash(new Color(1f, 0.15f, 0.15f));
        }

        public void NotifyCleared()
        {
            ClearedCount++;
            Flash(new Color(0.4f, 1f, 0.6f));
        }

        private void Update()
        {
            if (!_flashing) return;
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                _flashing = false;
                ApplyColor(Color.white);
            }
        }

        private void Flash(Color tint)
        {
            if (_renderer == null) return;
            _flashing = true;
            _flashTimer = 0.35f;
            ApplyColor(tint);
        }

        private void ApplyColor(Color tint)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, tint);
            _block.SetColor(BaseColorId, tint);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
