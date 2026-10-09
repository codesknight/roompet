using System.Collections.Generic;
using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Multi-touch input for the game layer.
    ///
    /// Why this exists instead of just using IMGUI buttons: IMGUI receives exactly ONE
    /// pointer — the primary touch is translated into a mouse event — so a player who
    /// holds the movement stick cannot also press jump. Reading <c>Input.touches</c>
    /// directly and hit-testing our own rects is the only way to get real simultaneous
    /// input, which every mobile game needs.
    ///
    /// Division of labour:
    ///   - menus and modal panels keep using ordinary IMGUI buttons (single touch is fine
    ///     there, and it keeps the desktop layout code untouched);
    ///   - movement, action buttons, camera drag and the runner's swipes go through here.
    ///
    /// The driver is installed automatically (<see cref="MobileTouchDriver"/>), so nothing
    /// has to be added to the scenes.
    /// </summary>
    public static class MobileTouch
    {
        // ------------------------------------------------------------------ gestures

        /// <summary>Tap/swipe recogniser for the play area, fed by the first free finger.</summary>
        public static readonly TouchGesture Gesture = new TouchGesture();

        /// <summary>Movement stick. Zone and drawing are the game's business.</summary>
        public static readonly VirtualJoystick Stick = new VirtualJoystick();

        /// <summary>Rect (screen pixels, origin top-left) where a touch may grab the stick.</summary>
        public static Rect StickZone;

        /// <summary>
        /// False while a panel owns the screen: no stick, no gestures, and every touch
        /// counts as UI so world objects are not clicked through the panel behind it.
        /// </summary>
        public static bool PlayInputEnabled = true;

        /// <summary>Set each frame by the game: true while the stick should accept a touch.</summary>
        public static bool StickEnabled = true;

        // ------------------------------------------------------------------- buttons

        private class ButtonState
        {
            public Rect Rect;
            public bool Visible;
            public string Label = "";
            public bool Pressed;     // went down this frame
            public bool Held;        // finger still on it right now
            public bool Released;    // lifted this frame

            /// <summary>Registration order, so "drawn last wins" is decidable when rects tie.</summary>
            public int Sequence;
        }

        private static int _buttonSequence;

        private static readonly Dictionary<string, ButtonState> Buttons =
            new Dictionary<string, ButtonState>();

        /// <summary>Owner of each live finger: "btn:&lt;id&gt;", "stick" or "gesture".</summary>
        private static readonly Dictionary<int, string> Owners = new Dictionary<int, string>();

        private static readonly List<int> SeenThisFrame = new List<int>(8);
        private static readonly List<int> GoneFingers = new List<int>(4);

        /// <summary>
        /// Registers a button's rect for this frame. Call from OnGUI — the rects are used
        /// from the NEXT frame's input pass, because IMGUI draw order and Update order do
        /// not line up. One frame of latency on a touch target is imperceptible.
        /// </summary>
        public static void RegisterButton(string id, Rect rect, bool visible, string label = "")
        {
            if (string.IsNullOrEmpty(id)) return;

            ButtonState state;
            if (!Buttons.TryGetValue(id, out state))
            {
                state = new ButtonState();
                Buttons[id] = state;
            }

            state.Rect = rect;
            state.Visible = visible;
            state.Sequence = ++_buttonSequence;
            if (!string.IsNullOrEmpty(label)) state.Label = label;
        }

        public static string LabelOf(string id)
        {
            ButtonState state;
            return Buttons.TryGetValue(id, out state) ? state.Label : "";
        }

        public static bool Pressed(string id)
        {
            ButtonState state;
            return Buttons.TryGetValue(id, out state) && state.Pressed;
        }

        public static bool Held(string id)
        {
            ButtonState state;
            return Buttons.TryGetValue(id, out state) && state.Held;
        }

        public static bool Released(string id)
        {
            ButtonState state;
            return Buttons.TryGetValue(id, out state) && state.Released;
        }

        public static Rect RectOf(string id)
        {
            ButtonState state;
            return Buttons.TryGetValue(id, out state) ? state.Rect : new Rect();
        }

        // --------------------------------------------------------------------- state

        /// <summary>
        /// True while any finger is being used by the UI. World clicks arrive through the
        /// physics-based <c>OnMouseDown</c>, which cannot see this by itself — so it is the
        /// game's job to ignore world clicks while it holds, otherwise tapping the movement
        /// stick would also click whatever bowl happens to be under it.
        /// </summary>
        public static bool UiTouchActive { get; private set; }

        /// <summary>Pointers being processed this frame (after the synthetic-mouse fallback).</summary>
        public static int ActiveTouchCount { get; private set; }

        /// <summary>True when the input came from a real touch rather than the mouse.</summary>
        public static bool UsingTouch { get; private set; }

        /// <summary>
        /// True when this finger belongs to a UI control (stick or button) rather than the
        /// play area. The camera rig uses it so dragging on the right of the screen orbits
        /// while the left thumb keeps moving the character.
        /// </summary>
        public static bool IsUiFinger(int fingerId)
        {
            string owner;
            if (!Owners.TryGetValue(fingerId, out owner)) return false;
            return owner == "stick" || owner == "ui" || owner.StartsWith("btn:");
        }

        // --------------------------------------------------------------- frame pass

        private static Vector2 _lastPointerPosition;

        /// <summary>Drives one frame of touch processing. Called by the auto-installed driver.</summary>
        public static void Tick()
        {
            ClearFrameFlags();

            bool touchMode = MobileUi.UseTouchControls;
            int count = touchMode ? Input.touchCount : 0;
            UsingTouch = count > 0;

            // In the editor (and on a desktop build with the phone layout forced on) the
            // mouse stands in for a finger, so the touch controls can be exercised without
            // a device. Unity's mouse origin is bottom-left, IMGUI's is top-left.
            bool synthesize = count == 0 && MobileUi.ForceTouchControls;
            if (synthesize) count = 1;

            ActiveTouchCount = count;
            UiTouchActive = false;
            SeenThisFrame.Clear();

            if (count == 0)
            {
                EndAllOwners();
                Gesture.ClearFrameFlags();
                return;
            }

            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < count; i++)
            {
                int fingerId;
                Vector2 position;
                TouchPhase phase;

                if (synthesize)
                {
                    fingerId = 0;
                    position = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                    if (Input.GetMouseButtonDown(0)) phase = TouchPhase.Began;
                    else if (Input.GetMouseButton(0)) phase = TouchPhase.Moved;
                    else if (Input.GetMouseButtonUp(0)) phase = TouchPhase.Ended;
                    else if (Owners.ContainsKey(fingerId)) phase = TouchPhase.Stationary;
                    else continue;   // nothing to do with a hovering mouse
                }
                else
                {
                    var touch = Input.GetTouch(i);
                    fingerId = touch.fingerId;
                    position = new Vector2(touch.position.x, Screen.height - touch.position.y);
                    phase = touch.phase;
                }

                SeenThisFrame.Add(fingerId);
                _lastPointerPosition = position;

                string owner;
                if (Owners.TryGetValue(fingerId, out owner))
                {
                    RouteToOwner(owner, fingerId, position, phase, dt);
                    continue;
                }

                if (phase == TouchPhase.Began) Claim(fingerId, position, dt);
            }

            // A finger can vanish without an Ended phase (app lost focus, OS gesture).
            // Without this the button it was holding would stay down forever.
            GoneFingers.Clear();
            foreach (var pair in Owners)
            {
                if (!SeenThisFrame.Contains(pair.Key)) GoneFingers.Add(pair.Key);
            }
            for (int i = 0; i < GoneFingers.Count; i++) EndOwner(GoneFingers[i], _lastPointerPosition);

            if (Stick.Active || AnyButtonHeld()) UiTouchActive = true;
            if (!PlayInputEnabled) UiTouchActive = true;

            Gesture.ClearFrameFlags();
        }

        private static void Claim(int fingerId, Vector2 position, float dt)
        {
            // A panel is up: the touch belongs to the UI. Nothing is claimed, but world
            // clicks are blocked (see UiTouchActive).
            if (!PlayInputEnabled)
            {
                Owners[fingerId] = "ui";
                UiTouchActive = true;
                return;
            }

            string buttonId = FindButtonAt(position);
            if (buttonId != null)
            {
                Owners[fingerId] = "btn:" + buttonId;
                var state = Buttons[buttonId];
                state.Pressed = true;
                state.Held = true;
                UiTouchActive = true;
                return;
            }

            if (StickEnabled && !Stick.Active && StickZone.width > 0f && StickZone.Contains(position))
            {
                Owners[fingerId] = "stick";
                Stick.Begin(fingerId, position);
                UiTouchActive = true;
                return;
            }

            // One gesture finger only; extra fingers elsewhere are ignored rather than
            // stealing the swipe from the one that started it.
            if (!Gesture.IsActive)
            {
                Owners[fingerId] = "gesture";
                Gesture.Begin(position);
                return;
            }

            Owners[fingerId] = "idle";
        }

        private static void RouteToOwner(string owner, int fingerId, Vector2 position, TouchPhase phase, float dt)
        {
            bool ended = phase == TouchPhase.Ended || phase == TouchPhase.Canceled;

            if (owner.StartsWith("btn:"))
            {
                string id = owner.Substring(4);
                ButtonState state;
                if (Buttons.TryGetValue(id, out state))
                {
                    // Sliding off the control releases it, which is what players expect from
                    // a button they can see.
                    state.Held = !ended && state.Rect.Contains(position);
                    if (ended) state.Released = true;
                }
                UiTouchActive = true;
                if (ended) Owners.Remove(fingerId);
                return;
            }

            if (owner == "stick")
            {
                if (ended) Stick.End();
                else Stick.Move(position);
                UiTouchActive = true;
                if (ended) Owners.Remove(fingerId);
                return;
            }

            if (owner == "gesture")
            {
                if (ended)
                {
                    Gesture.End(position, dt);
                    Owners.Remove(fingerId);
                }
                else
                {
                    Gesture.Move(position, dt);
                }
                return;
            }

            if (ended) Owners.Remove(fingerId);
        }

        private static void EndOwner(int fingerId, Vector2 position)
        {
            string owner;
            if (!Owners.TryGetValue(fingerId, out owner)) return;
            Owners.Remove(fingerId);

            if (owner == "stick")
            {
                Stick.End();
                return;
            }

            if (owner == "gesture")
            {
                Gesture.End(position, 0f);
                return;
            }

            if (owner.StartsWith("btn:"))
            {
                ButtonState state;
                if (Buttons.TryGetValue(owner.Substring(4), out state))
                {
                    state.Held = false;
                    state.Released = true;
                }
            }
        }

        private static void EndAllOwners()
        {
            if (Owners.Count == 0) return;

            foreach (var pair in Owners)
            {
                if (pair.Value == "stick") Stick.End();
                else if (pair.Value == "gesture") Gesture.End(Gesture.Position, 0f);
            }
            Owners.Clear();
        }

        /// <summary>
        /// The button under a point.
        ///
        /// Two rules, both learned the hard way. <b>Smallest wins</b>: a small button sitting on
        /// top of a big one has to be the one that reacts, and a dictionary iteration order (which
        /// is what this used to be) means the big one sometimes won and the small one "did not
        /// work". <b>Newest breaks ties</b>: the thing drawn last is the thing on top, exactly like
        /// IMGUI.
        /// </summary>
        private static string FindButtonAt(Vector2 position)
        {
            string best = null;
            float bestArea = float.MaxValue;
            int bestSequence = -1;

            foreach (var pair in Buttons)
            {
                if (!pair.Value.Visible) continue;
                if (!pair.Value.Rect.Contains(position)) continue;

                float area = pair.Value.Rect.width * pair.Value.Rect.height;
                bool better = area < bestArea - 0.01f ||
                              (Mathf.Abs(area - bestArea) <= 0.01f && pair.Value.Sequence > bestSequence);
                if (!better) continue;

                best = pair.Key;
                bestArea = area;
                bestSequence = pair.Value.Sequence;
            }

            return best;
        }

        private static bool AnyButtonHeld()
        {
            foreach (var pair in Buttons)
            {
                if (pair.Value.Held) return true;
            }
            return false;
        }

        private static void ClearFrameFlags()
        {
            foreach (var pair in Buttons)
            {
                pair.Value.Pressed = false;
                pair.Value.Released = false;
                pair.Value.Held = false;   // recomputed while routing live pointers
            }
        }

        /// <summary>Drops all transient state, e.g. when a scene or mini game is loaded.</summary>
        public static void Reset()
        {
            Buttons.Clear();
            Owners.Clear();
            Gesture.End(Gesture.Position, 0f);
            Stick.End();
            UiTouchActive = false;
            PlayInputEnabled = true;
            StickEnabled = true;
        }
    }

    /// <summary>
    /// Installs <see cref="MobileTouch"/>'s per-frame pass without touching the scenes.
    ///
    /// The execution order matters: -200 runs before the game scripts (order 0), so the
    /// input is already resolved when they read it in their own Update.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MobileTouchDriver : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("~MobileTouch");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<MobileTouchDriver>();
        }

        private static MobileTouchDriver _instance;

        private void Update() => MobileTouch.Tick();
    }
}
