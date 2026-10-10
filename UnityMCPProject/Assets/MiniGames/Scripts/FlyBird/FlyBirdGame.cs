using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// One-button flying: tap to flap, get through the gaps.
    ///
    /// The third game in the collection, and deliberately the smallest to explain: one input, one
    /// verb, one fail state. It exists because the other two cover different things — the runner
    /// is about lanes and reaction, the puzzle is about thinking — and this is the one that fits
    /// in thirty seconds in a queue.
    ///
    /// Everything is primitives and code, like the rest of the project: no sprite sheet, no
    /// tileset, no prefabs to keep in step with the scripts. The bird is two spheres and a
    /// triangle, the pipes are boxes, and the sky is the camera's clear colour.
    /// </summary>
    public class FlyBirdGame : MonoBehaviour
    {
        // ------------------------------------------------------------------ state

        public enum Phase { Ready, Flying, Dead }

        public Phase State { get; private set; } = Phase.Ready;

        /// <summary>Pipes cleared. This is the score and the coin count.</summary>
        public int Score { get; private set; }

        /// <summary>Best score of this session, for the result panel.</summary>
        public int Best { get; private set; }

        private FlyBirdSettings _settings = FlyBirdSettings.Default;

        private float _height;
        private float _velocity;
        private float _nextSpawn;
        private float _deathAt;

        private Transform _bird;
        private Transform _birdWing;
        private Camera _camera;
        private float _lastAspect;
        private readonly System.Collections.Generic.List<Pipe> _pipes =
            new System.Collections.Generic.List<Pipe>();

        private readonly System.Random _rng = new System.Random();

        private class Pipe
        {
            public Transform Root;
            public float Centre;
            public float Gap;
            public bool Scored;
        }

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
            ResetRun();
        }

        private void BuildWorld()
        {
            // Camera: a flat side-on view, which is all a flappy game needs.
            var existing = Camera.main;
            if (existing == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                existing = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            existing.orthographic = true;
            existing.transform.position = new Vector3(0f, 0f, -10f);
            existing.transform.rotation = Quaternion.identity;
            existing.clearFlags = CameraClearFlags.SolidColor;
            existing.backgroundColor = new Color(0.36f, 0.58f, 0.82f);
            existing.nearClipPlane = 0.1f;
            existing.farClipPlane = 60f;
            _camera = existing;
            FitCamera();

            // A lamp so the standard shader has something to light: without it everything is flat
            // black, which is what happened the first time a scene in this project had no light.
            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.05f;
                light.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.62f);

            // Ground.
            var ground = Box("Ground", new Vector3(0f, -_settings.PlayHeight - 1.6f, 0f),
                new Vector3(80f, 2.4f, 2f), new Color(0.35f, 0.62f, 0.32f));
            ground.gameObject.isStatic = true;

            _bird = BuildBird();
        }

        private Transform BuildBird()
        {
            var root = new GameObject("Bird").transform;
            root.SetParent(transform, false);

            // Prefer the Kenney chick over the sphere-and-box bird. The whole-body tilt in
            // ApplyBird carries the animation, so the separate flapping wing is dropped.
            var model = DshMobile.KenneyModel.Load("animal-chick", root, 0.55f, centerVertically: true);
            if (model != null)
            {
                model.localRotation = Quaternion.Euler(0f, -90f, 0f);   // face +X, toward the pipes
                _birdWing = null;
                return root;
            }

            var body = Sphere("Body", Vector3.zero, 0.42f, new Color(1f, 0.84f, 0.3f));
            body.SetParent(root, false);
            body.localPosition = Vector3.zero;

            var head = Sphere("Head", Vector3.zero, 0.30f, new Color(1f, 0.88f, 0.42f));
            head.SetParent(root, false);
            head.localPosition = new Vector3(0.30f, 0.20f, 0f);

            // A beak: two boxes, because a cone primitive is a capsule and looks like a nose.
            var beak = Box("Beak", Vector3.zero, new Vector3(0.26f, 0.12f, 0.12f),
                new Color(1f, 0.55f, 0.18f));
            beak.SetParent(root, false);
            beak.localPosition = new Vector3(0.62f, 0.16f, 0f);

            var eye = Sphere("Eye", Vector3.zero, 0.07f, new Color(0.12f, 0.1f, 0.12f));
            eye.SetParent(root, false);
            eye.localPosition = new Vector3(0.42f, 0.30f, -0.16f);

            // The wing is kept so it can flap: the whole animation is a rotation.
            _birdWing = Box("Wing", Vector3.zero, new Vector3(0.34f, 0.10f, 0.3f),
                new Color(0.98f, 0.72f, 0.22f));
            _birdWing.SetParent(root, false);
            _birdWing.localPosition = new Vector3(-0.12f, 0.06f, 0f);

            return root;
        }

        private Transform Sphere(string name, Vector3 position, float diameter, Color color)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * diameter;
            Tint(sphere, color);
            Destroy(sphere.GetComponent<Collider>());
            return sphere.transform;
        }

        private Transform Box(string name, Vector3 position, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            Tint(box, color);
            Destroy(box.GetComponent<Collider>());
            return box.transform;
        }

        private static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            // One material per object: they are few, and a shared material would make every pipe
            // change colour when one of them did.
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.25f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ run control

        public void ResetRun()
        {
            ClearPipes();

            Score = 0;
            _height = 0.4f;
            _velocity = 0f;
            _nextSpawn = 4.5f;
            _lastCentre = _height;      // the run starts from wherever the bird starts
            State = Phase.Ready;

            if (_bird != null) _bird.position = new Vector3(BirdX, _height, 0f);
            DshMobile.MobileHaptics.Light();
        }

        private void ClearPipes()
        {
            for (int i = 0; i < _pipes.Count; i++)
            {
                if (_pipes[i].Root != null) Destroy(_pipes[i].Root.gameObject);
            }
            _pipes.Clear();
        }

        /// <summary>Called by the HUD's button as well as by a tap, so both start the same run.</summary>
        public void Flap()
        {
            if (State == Phase.Dead)
            {
                // A tap after death restarts, but not instantly: a run that ends where the finger
                // already is would otherwise restart before the player sees what happened.
                if (Time.time - _deathAt > 0.6f) ResetRun();
                return;
            }

            State = Phase.Flying;
            _velocity = _settings.FlapSpeed;
            DshMobile.MobileHaptics.Light();

            if (_birdWing != null) _wingFlip = 1f;
        }

        private float _wingFlip;

        /// <summary>
        /// Frames the play area for the current shape of the screen.
        ///
        /// Two constraints, whichever is larger: the whole flight corridor has to be visible
        /// vertically, and the bird has to have room in front of it horizontally — a portrait
        /// screen is narrow, and a camera fitted to height alone leaves the bird, and most of the
        /// reaction distance, outside the frame.
        /// </summary>
        private void FitCamera()
        {
            if (_camera == null) return;

            _lastAspect = _camera.aspect;
            float byHeight = _settings.PlayHeight + 0.9f;
            float byWidth = 5.3f / Mathf.Max(0.2f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(byHeight, byWidth);
        }

        /// <summary>Horizontal half-extent of the view, in world units.</summary>
        private float HalfWidth => _camera != null
            ? _camera.orthographicSize * _camera.aspect
            : 5.3f;

        /// <summary>Where the bird sits: a little inside the left edge, whatever the shape.</summary>
        private float BirdX => -HalfWidth + 1.1f;

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            // A phone can be turned, and a portrait window is only half as wide as it is tall: the
            // first version kept a fixed orthographic size, which on a portrait phone put the bird
            // itself off the left of the screen. The fit is recomputed whenever the shape changes.
            if (_camera != null && !Mathf.Approximately(_lastAspect, _camera.aspect)) FitCamera();

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) ||
                Input.GetKeyDown(KeyCode.UpArrow))
            {
                Flap();
            }

            // Mouse/touch now arrive through the uGUI HUD's full-screen flap button (its onClick
            // calls Flap), which is the one place that knows where the buttons are — the panels
            // drawn above the button swallow their own taps, so a tap on "再来一次" no longer
            // flaps the bird at the same moment. Keyboard stays here.

            if (State == Phase.Dead)
            {
                // Let it fall, so the failure is visible rather than a freeze — but stop it on the
                // ground instead of dropping it out of the bottom of the screen, where the player
                // would be looking at an empty sky wondering what they hit.
                float floor = -_settings.PlayHeight + _settings.GroundHeight;
                if (_height > floor)
                {
                    _velocity -= _settings.Gravity * Time.deltaTime * 0.5f;
                    _height = Mathf.Max(floor, _height + _velocity * Time.deltaTime * 0.5f);
                }
                else
                {
                    _velocity = 0f;
                }

                ApplyBird();
                return;
            }

            bool flying = State == Phase.Flying;

            if (flying)
            {
                // The flap itself is applied on the press (see Flap), and the step only integrates:
                // passing "held" through here would turn the game into "hold the button to hover",
                // which is a different and much easier game than the one the taps promise.
                FlyBirdRules.Step(ref _height, ref _velocity, Time.deltaTime, false, _settings);
            }

            ApplyBird();

            if (flying)
            {
                TickPipes(Time.deltaTime);
                if (_height < -_settings.PlayHeight + _settings.GroundHeight) Die();
            }
        }

        private void ApplyBird()
        {
            if (_bird == null) return;

            // Clamp the visible height only: the physics keeps the real value, so the game over
            // test is the honest one.
            float shown = Mathf.Clamp(_height, -_settings.PlayHeight, _settings.PlayHeight + 2f);
            _bird.position = new Vector3(BirdX, shown, 0f);
            _bird.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Clamp(_velocity * 5f, -35f, 32f));

            if (_birdWing != null && _wingFlip > 0f)
            {
                _wingFlip -= Time.deltaTime * 6f;
                _birdWing.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_wingFlip * 8f) * 45f);
            }
        }

        private void TickPipes(float dt)
        {
            float speed = FlyBirdRules.SpeedFor(Score, _settings);

            for (int i = _pipes.Count - 1; i >= 0; i--)
            {
                var pipe = _pipes[i];
                if (pipe.Root == null)
                {
                    _pipes.RemoveAt(i);
                    continue;
                }

                pipe.Root.position += Vector3.left * (speed * dt);

                if (!pipe.Scored && pipe.Root.position.x < BirdX)
                {
                    pipe.Scored = true;
                    Score++;
                    if (Score > Best) Best = Score;
                    DshMobile.MobileHaptics.Light();
                }

                if (pipe.Root.position.x < BirdX - 8f)
                {
                    Destroy(pipe.Root.gameObject);
                    _pipes.RemoveAt(i);
                    continue;
                }

                if (Mathf.Abs(pipe.Root.position.x - BirdX) < 0.75f &&
                    !FlyBirdRules.PassesGap(_height, 0.30f, pipe.Centre, pipe.Gap))
                {
                    Die();
                    return;
                }
            }

            _nextSpawn -= speed * dt;
            if (_nextSpawn <= 0f)
            {
                SpawnPipe();

                // The spacing is a *rate*, not a distance: it grows with the speed so the pipes keep
                // arriving at a rhythm a thumb can play (see FlyBirdRules.PipeSeconds).
                _nextSpawn = FlyBirdRules.SpacingFor(Score, _settings);
            }
        }

        /// <summary>
        /// Places the next pipe pair.
        ///
        /// The gap's height is not a free random number: it is drawn from the window the bird can
        /// physically fly to from the last gap, given the time the next pipe takes to arrive (see
        /// <see cref="FlyBirdRules.ReachableCentre"/>). The first version rolled uniformly over the
        /// whole play area, which produced pairs of pipes 14 units apart when the bird can travel
        /// about 4 — the run was already lost, and it felt exactly as random as it was.
        /// </summary>
        private void SpawnPipe()
        {
            float gap = FlyBirdRules.GapFor(Score, _settings);
            float roll = (float)_rng.NextDouble();
            float centre = FlyBirdRules.NextGapCentre(roll, _lastCentre, gap, Score, _settings);
            _lastCentre = centre;

            var root = new GameObject("Pipe").transform;
            root.SetParent(transform, false);
            root.position = new Vector3(HalfWidth + 1.6f, 0f, 0f);

            var green = new Color(0.28f, 0.60f, 0.32f);
            var greenLight = new Color(0.46f, 0.74f, 0.42f);
            float bottomTop = centre - gap * 0.5f;
            float topBottom = centre + gap * 0.5f;

            float worldBottom = -_settings.PlayHeight - 1.6f;
            float bottomHeight = bottomTop - worldBottom;
            var bottom = Box("PipeBottom", Vector3.zero, new Vector3(1.1f, bottomHeight, 1.1f), green);
            bottom.SetParent(root, false);
            bottom.localPosition = new Vector3(0f, worldBottom + bottomHeight * 0.5f, 0f);

            // Mario-style lip: a wider rim at the mouth of each pipe, so the pipe reads as a 3D
            // pipe rather than a flat green slab.
            var bottomLip = Box("PipeBottomLip", Vector3.zero, new Vector3(1.42f, 0.26f, 1.42f), greenLight);
            bottomLip.SetParent(root, false);
            bottomLip.localPosition = new Vector3(0f, bottomTop - 0.13f, 0f);

            float worldTop = _settings.PlayHeight + 2.4f;
            float topHeight = worldTop - topBottom;
            var top = Box("PipeTop", Vector3.zero, new Vector3(1.1f, topHeight, 1.1f), green);
            top.SetParent(root, false);
            top.localPosition = new Vector3(0f, topBottom + topHeight * 0.5f, 0f);

            var topLip = Box("PipeTopLip", Vector3.zero, new Vector3(1.42f, 0.26f, 1.42f), greenLight);
            topLip.SetParent(root, false);
            topLip.localPosition = new Vector3(0f, topBottom + 0.13f, 0f);

            _pipes.Add(new Pipe { Root = root, Centre = centre, Gap = gap });
        }

        /// <summary>The gap centre of the last pipe placed: where the bird has to come from.</summary>
        private float _lastCentre;

        private void Die()
        {
            if (State == Phase.Dead) return;

            State = Phase.Dead;
            _deathAt = Time.time;
            DshMobile.MobileHaptics.Heavy();

            // The coins go in immediately rather than on the result screen: the player earned
            // them the moment they cleared the last pipe, and a crash should not cost the run.
            int coins = FlyBirdRules.CoinsFor(Score);
            if (coins > 0) DshMobile.PetWallet.Add(coins);
        }

        /// <summary>Coins this run has earned, for the result panel.</summary>
        public int RunCoins => FlyBirdRules.CoinsFor(Score);

        /// <summary>Goes home, the same way the runner does.</summary>
        public void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the bird game");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }
    }
}
