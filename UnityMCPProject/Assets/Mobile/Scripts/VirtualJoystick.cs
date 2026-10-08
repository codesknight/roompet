using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// A floating virtual stick: the base appears wherever the thumb lands inside the
    /// zone, so the player never has to look for it.
    ///
    /// Logic only — <see cref="Draw"/> paints it and <see cref="MobileTouch"/> feeds it
    /// positions. Keeping the math separate means dead zone and clamping are testable.
    /// </summary>
    public class VirtualJoystick
    {
        /// <summary>Radius of the stick's travel, in design pixels.</summary>
        public float Radius = 68f;

        /// <summary>Fraction of the radius that produces no output (avoids drift).</summary>
        public float DeadZone = 0.16f;

        /// <summary>Where the base is drawn while the stick is held.</summary>
        public Vector2 Origin { get; private set; }

        /// <summary>Current thumb position, clamped to <see cref="Radius"/>.</summary>
        public Vector2 Knob { get; private set; }

        /// <summary>True while a finger owns the stick.</summary>
        public bool Active { get; private set; }

        /// <summary>Output in [-1,1] on both axes, already dead-zoned and normalised.</summary>
        public Vector2 Value { get; private set; }

        private int _fingerId = -1;

        public void Begin(int fingerId, Vector2 position)
        {
            _fingerId = fingerId;
            Origin = position;
            Knob = position;
            Active = true;
            Value = Vector2.zero;
        }

        public void Move(Vector2 position)
        {
            if (!Active) return;

            Vector2 offset = position - Origin;
            float radius = Mathf.Max(1f, Radius);
            if (offset.magnitude > radius) offset = offset.normalized * radius;
            Knob = Origin + offset;

            // Screen Y grows downwards while gameplay Y grows upwards.
            var raw = new Vector2(offset.x / radius, -offset.y / radius);
            float magnitude = raw.magnitude;
            if (magnitude <= DeadZone)
            {
                Value = Vector2.zero;
                return;
            }

            // Rescale so the value ramps from 0 at the dead-zone edge to 1 at the rim:
            // without this there is a visible jump the moment the thumb leaves the centre.
            float scaled = (magnitude - DeadZone) / Mathf.Max(0.0001f, 1f - DeadZone);
            Value = raw.normalized * Mathf.Clamp01(scaled);
        }

        public void End()
        {
            Active = false;
            _fingerId = -1;
            Value = Vector2.zero;
        }

        /// <summary>The finger this stick belongs to, or -1.</summary>
        public int FingerId => _fingerId;

        /// <summary>True when the given point is inside the stick's grab zone.</summary>
        public bool Contains(Vector2 point, Vector2 zoneSize)
        {
            float reach = Radius * 1.6f;
            return Mathf.Abs(point.x - Origin.x) <= reach && Mathf.Abs(point.y - Origin.y) <= reach;
        }
    }
}
