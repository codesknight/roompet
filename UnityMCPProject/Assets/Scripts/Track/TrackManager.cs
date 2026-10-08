using System.Collections.Generic;
using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// Endless recycled forest track. Segment shells are pooled GameObjects; the layout of
    /// each segment comes from <see cref="TrackPlanner"/> (deterministic per level seed),
    /// and every obstacle, collectable and scenery prop is a CC0 Kenney model loaded
    /// through <see cref="PropLibrary"/>.
    ///
    /// Pooling is keyed by a string so a recycled instance always carries the same model:
    /// reusing a "rock" instance where an "log" is expected would leave the wrong mesh in
    /// place.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class TrackManager : MonoBehaviour
    {
        /// <summary>Seconds of travel the player should get between obstacle rows.</summary>
        public const float MinReactionTime = 0.6f;
        public const float MinGap = 9f;
        public const float MaxGap = 30f;

        private const float FloorThickness = 0.6f;
        private const float VergeWidth = 22f;
        private const float FenceSpacing = 4.5f;

        private static readonly string[] BlockerProps = PropLibrary.TallBlockers;
        private static readonly string[] FruitProps = PropLibrary.Fruit;
        private static readonly string[] TreeProps = PropLibrary.Trees;
        private static readonly string[] BushProps = PropLibrary.Undergrowth;
        private static readonly string[] FenceProps = PropLibrary.Fences;

        private const string LogProp = "Runner/Nature/log";

        private class Segment
        {
            public GameObject Root;
            public int Index;
            public float StartZ;
            public float EndZ;
            public readonly List<GameObject> Content = new List<GameObject>();
        }

        private readonly List<Segment> _active = new List<Segment>();
        private readonly Stack<GameObject> _segmentPool = new Stack<GameObject>();
        private readonly Dictionary<string, Stack<GameObject>> _pools = new Dictionary<string, Stack<GameObject>>();
        private readonly List<Pickup> _pickups = new List<Pickup>();

        private LevelDefinition _level;
        private int _nextIndex;
        private Transform _trackRoot;

        public int ActiveSegments => _active.Count;
        public LevelDefinition Level => _level;

        private void Awake()
        {
            var root = new GameObject("TrackRuntime");
            root.transform.SetParent(transform, false);
            _trackRoot = root.transform;
        }

        /// <summary>Clears the track and lays it out again for a level.</summary>
        public void Rebuild(LevelDefinition level, float fromDistance)
        {
            _level = level ?? LevelLibrary.Endless;

            for (int i = _active.Count - 1; i >= 0; i--) RecycleSegment(_active[i]);
            _active.Clear();
            _pickups.Clear();

            _nextIndex = Mathf.Max(0, Mathf.FloorToInt(fromDistance / GameConfig.SegmentLength));
            for (int i = 0; i < GameConfig.SegmentsAhead; i++) BuildNextSegment();
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            if (gm.State != GameState.Playing && gm.State != GameState.Paused) return;

            float playerZ = gm.Player.transform.position.z;

            while (_nextIndex * GameConfig.SegmentLength <
                   playerZ + GameConfig.SegmentsAhead * GameConfig.SegmentLength)
            {
                BuildNextSegment();
            }

            float cutoff = playerZ - GameConfig.SegmentsBehind * GameConfig.SegmentLength;
            while (_active.Count > 0 && _active[0].EndZ < cutoff)
            {
                RecycleSegment(_active[0]);
                _active.RemoveAt(0);
            }

            float magnetRadius = gm.PowerUps != null && gm.PowerUps.MagnetActive ? gm.PowerUps.MagnetRadius : 0f;
            Vector3 playerPos = gm.Player.transform.position;
            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                var pickup = _pickups[i];
                if (pickup == null || pickup.Collected || !pickup.gameObject.activeSelf)
                {
                    _pickups.RemoveAt(i);
                    continue;
                }

                pickup.Tick(Time.deltaTime, magnetRadius, playerPos);
            }
        }

        // ------------------------------------------------------------------ segments

        private void BuildNextSegment()
        {
            int index = _nextIndex++;
            var segment = new Segment
            {
                Index = index,
                StartZ = index * GameConfig.SegmentLength,
                Root = GetSegmentObject()
            };
            segment.EndZ = segment.StartZ + GameConfig.SegmentLength;
            segment.Root.transform.SetParent(_trackRoot, false);
            segment.Root.transform.localPosition = new Vector3(0f, 0f, segment.StartZ);

            _active.Add(segment);
            PopulateSegment(segment);
        }

        private void RecycleSegment(Segment segment)
        {
            for (int i = 0; i < segment.Content.Count; i++)
            {
                var go = segment.Content[i];
                if (go == null) continue;

                for (int p = _pickups.Count - 1; p >= 0; p--)
                {
                    if (_pickups[p] != null && _pickups[p].gameObject == go) _pickups.RemoveAt(p);
                }

                Release(go);
            }

            segment.Content.Clear();
            segment.Root.SetActive(false);
            segment.Root.transform.SetParent(transform, false);
            _segmentPool.Push(segment.Root);
        }

        private GameObject GetSegmentObject()
        {
            if (_segmentPool.Count > 0)
            {
                var pooled = _segmentPool.Pop();
                pooled.SetActive(true);
                return pooled;
            }

            return BuildSegmentShell();
        }

        /// <summary>Dirt path down the middle with grass verges either side.</summary>
        private GameObject BuildSegmentShell()
        {
            float pathWidth = GameConfig.LaneCount * GameConfig.LaneWidth + 0.6f;
            var root = new GameObject("Segment");

            var verge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            verge.name = "Verge";
            verge.transform.SetParent(root.transform, false);
            verge.transform.localScale = new Vector3(VergeWidth, FloorThickness, GameConfig.SegmentLength);
            verge.transform.localPosition = new Vector3(0f, -0.02f - FloorThickness * 0.5f,
                GameConfig.SegmentLength * 0.5f);
            SetMaterial(verge, RunnerMaterials.Ground);
            StripCollider(verge);

            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Path";
            path.transform.SetParent(root.transform, false);
            path.transform.localScale = new Vector3(pathWidth, FloorThickness, GameConfig.SegmentLength);
            path.transform.localPosition = new Vector3(0f, -FloorThickness * 0.5f, GameConfig.SegmentLength * 0.5f);
            SetMaterial(path, RunnerMaterials.Path);
            StripCollider(path);

            return root;
        }

        // ------------------------------------------------------------------- content

        private void PopulateSegment(Segment segment)
        {
            PlaceFenceLine(segment);
            ScatterScenery(segment);

            var steps = TrackPlanner.PlanSegment(_level, segment.Index);
            for (int i = 0; i < steps.Length; i++)
            {
                var step = steps[i];

                if (step.HasObstacle && step.BlockedLanes != null)
                {
                    for (int lane = 0; lane < step.BlockedLanes.Length; lane++)
                    {
                        SpawnObstacle(segment, step.Z, step.BlockedLanes[lane], step.Kind,
                            TrackPlanner.HashSeed(_level.Seed, segment.Index * 977 + i * 31 + lane));
                    }
                }

                if (step.CoinLane >= 0) PlaceFruitRun(segment, step.Z, step.CoinLane, step.CoinArc);
                if (step.PowerUpLane >= 0) PlacePowerUp(segment, step.Z + 2f, step.PowerUpLane, step.PowerUp);
            }
        }

        private void PlaceFenceLine(Segment segment)
        {
            float pathWidth = GameConfig.LaneCount * GameConfig.LaneWidth + 0.6f;
            float fenceX = pathWidth * 0.5f + 0.4f;
            int posts = Mathf.Max(1, Mathf.RoundToInt(GameConfig.SegmentLength / FenceSpacing));
            var rng = new System.Random(TrackPlanner.HashSeed(_level != null ? _level.Seed : 0, 51000 + segment.Index));

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < posts; i++)
                {
                    float z = (i + 0.5f) * (GameConfig.SegmentLength / posts);
                    string prop = FenceProps[rng.Next(FenceProps.Length)];
                    PlaceProp(segment, prop, new Vector3(side * fenceX, 0f, z), 2.4f,
                        (float)rng.NextDouble() * 360f);
                }
            }
        }

        /// <summary>Nature dressing on the verges, deterministic per segment.</summary>
        private void ScatterScenery(Segment segment)
        {
            var rng = new System.Random(TrackPlanner.HashSeed(_level != null ? _level.Seed : 0, 70000 + segment.Index));
            float pathHalf = (GameConfig.LaneCount * GameConfig.LaneWidth + 0.6f) * 0.5f;
            float vergeOuter = VergeWidth * 0.5f - 2f;

            int trees = 3 + rng.Next(3);
            for (int i = 0; i < trees; i++)
            {
                float side = rng.Next(2) == 0 ? -1f : 1f;
                float x = side * Mathf.Lerp(pathHalf + 3.2f, vergeOuter, (float)rng.NextDouble());
                float z = (float)rng.NextDouble() * GameConfig.SegmentLength;
                float size = 4.0f + (float)rng.NextDouble() * 3.5f;
                PlaceProp(segment, TreeProps[rng.Next(TreeProps.Length)], new Vector3(x, 0f, z), size,
                    (float)rng.NextDouble() * 360f);
            }

            int bushes = 8 + rng.Next(5);
            for (int i = 0; i < bushes; i++)
            {
                float side = rng.Next(2) == 0 ? -1f : 1f;
                float x = side * Mathf.Lerp(pathHalf + 0.2f, pathHalf + 3.4f, (float)rng.NextDouble());
                float z = (float)rng.NextDouble() * GameConfig.SegmentLength;
                float size = 0.5f + (float)rng.NextDouble() * 0.9f;
                PlaceProp(segment, BushProps[rng.Next(BushProps.Length)], new Vector3(x, 0f, z), size,
                    (float)rng.NextDouble() * 360f);
            }
        }

        private GameObject PlaceProp(Segment segment, string prop, Vector3 localPosition, float size, float yaw)
        {
            string key = "prop:" + prop;
            var instance = TakeFromPool(key);
            if (instance == null)
            {
                instance = PropLibrary.Spawn(prop, null, Vector3.zero, size, yaw);
                if (instance == null) return null;
                instance.AddComponent<PropLibrary.PooledItem>().Key = key;
            }

            instance.SetActive(true);
            instance.transform.SetParent(segment.Root.transform, false);
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            ResizeTo(instance, size);
            AlignBase(instance, localPosition);
            segment.Content.Add(instance);
            return instance;
        }

        private void SpawnObstacle(Segment segment, float z, int lane, ObstacleKind kind, int seed)
        {
            var rng = new System.Random(seed);
            float x = GameConfig.LaneX(lane);
            float localZ = z - segment.StartZ;

            string prop;
            float modelSize;
            float yaw;
            Vector3 colliderSize;
            Vector3 colliderCentre;
            float topY;
            string message;

            switch (kind)
            {
                case ObstacleKind.JumpBarrier:
                    prop = LogProp;
                    modelSize = 2.5f;
                    yaw = 90f;
                    colliderSize = new Vector3(2.2f, 0.7f, 1.1f);
                    colliderCentre = new Vector3(0f, 0.32f, 0f);
                    topY = 0.62f;
                    message = "没跳过这根木头";
                    break;

                case ObstacleKind.SlideBarrier:
                    prop = LogProp;
                    modelSize = 2.5f;
                    yaw = 90f;
                    colliderSize = new Vector3(2.2f, 0.7f, 1.1f);
                    colliderCentre = new Vector3(0f, 1.62f, 0f);
                    topY = 1.95f;
                    message = "没滑铲过去";
                    break;

                default:
                    prop = BlockerProps[Mathf.Abs(rng.Next()) % BlockerProps.Length];
                    modelSize = 2.2f;
                    yaw = (float)rng.NextDouble() * 360f;
                    colliderSize = new Vector3(2.05f, 2.0f, 1.3f);
                    colliderCentre = new Vector3(0f, 1.0f, 0f);
                    topY = 2.0f;
                    message = "撞上障碍物";
                    break;
            }

            string key = "obstacle:" + prop + ":" + kind;
            var root = TakeFromPool(key);
            if (root == null)
            {
                root = new GameObject("Obstacle");
                root.AddComponent<BoxCollider>().isTrigger = true;
                root.AddComponent<ObstacleMarker>();
                var model = PropLibrary.Spawn(prop, root.transform, Vector3.zero, modelSize, yaw);
                if (model == null)
                {
                    Destroy(root);
                    return;
                }
                root.AddComponent<PropLibrary.PooledItem>().Key = key;
            }

            root.SetActive(true);
            root.transform.SetParent(segment.Root.transform, false);
            root.transform.localPosition = new Vector3(x, 0f, localZ);

            var box = root.GetComponent<BoxCollider>();
            box.size = colliderSize;
            box.center = colliderCentre;

            // The model child sits at the collider's centre so it lines up with the hitbox.
            var child = root.transform.childCount > 0 ? root.transform.GetChild(0) : null;
            if (child != null)
            {
                child.localPosition = colliderCentre;
                child.localRotation = Quaternion.Euler(0f, yaw, 0f);
                ResizeTo(child.gameObject, modelSize);
            }

            root.GetComponent<ObstacleMarker>().Configure(kind, topY, message);
            segment.Content.Add(root);
        }

        private void PlaceFruitRun(Segment segment, float z, int lane, bool arc)
        {
            int count = arc ? 5 : 4;
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * TrackPlanner.CoinSpacing;
                float y = GameConfig.PlayerHalfHeight;
                if (arc)
                {
                    float t = i / (float)(count - 1);
                    y += Mathf.Sin(t * Mathf.PI) * 1.5f;
                }

                int pick = Mathf.Abs(TrackPlanner.HashSeed(lane * 31 + i, segment.Index)) % FruitProps.Length;
                PlacePickup(segment, z + offset, lane, y, FruitProps[pick], true, PowerUpKind.Shield);
            }
        }

        private void PlacePowerUp(Segment segment, float z, int lane, PowerUpKind power)
        {
            // A bigger, rarer fruit so power-ups read differently from coins.
            string prop = power == PowerUpKind.SlowMotion ? "Runner/Items/watermelon" : "Runner/Items/corn";
            PlacePickup(segment, z, lane, GameConfig.PlayerHalfHeight + 0.2f, prop, false, power);
        }

        private void PlacePickup(Segment segment, float z, int lane, float y, string prop, bool isCoin,
            PowerUpKind power)
        {
            float size = isCoin ? 0.52f : 0.95f;
            string key = "pickup:" + prop;

            var root = TakeFromPool(key);
            if (root == null)
            {
                root = new GameObject(isCoin ? "Fruit" : "PowerUp");
                root.AddComponent<SphereCollider>().isTrigger = true;
                root.AddComponent<Pickup>();
                var model = PropLibrary.Spawn(prop, root.transform, Vector3.zero, size);
                if (model == null)
                {
                    Destroy(root);
                    return;
                }
                model.transform.localPosition = Vector3.zero;
                root.AddComponent<PropLibrary.PooledItem>().Key = key;
            }

            root.SetActive(true);
            root.transform.SetParent(segment.Root.transform, false);

            var sphere = root.GetComponent<SphereCollider>();
            sphere.radius = isCoin ? 0.85f : 1.15f;
            sphere.center = Vector3.zero;

            var pickup = root.GetComponent<Pickup>();
            if (isCoin) pickup.ConfigureCoin(); else pickup.ConfigurePowerUp(power);

            var local = new Vector3(GameConfig.LaneX(lane), y, z - segment.StartZ);
            pickup.ResetForPool(local);

            segment.Content.Add(root);
            _pickups.Add(pickup);
        }

        // ---------------------------------------------------------------------- pool

        private GameObject TakeFromPool(string key)
        {
            if (!_pools.TryGetValue(key, out var stack) || stack.Count == 0) return null;
            return stack.Pop();
        }

        private void Release(GameObject go)
        {
            go.SetActive(false);

            var tag = go.GetComponent<PropLibrary.PooledItem>();
            if (tag == null)
            {
                Destroy(go);
                return;
            }

            go.transform.SetParent(transform, false);
            if (!_pools.TryGetValue(tag.Key, out var stack))
            {
                stack = new Stack<GameObject>();
                _pools[tag.Key] = stack;
            }
            stack.Push(go);
        }

        // --------------------------------------------------------------------- utils

        /// <summary>Rescales an existing instance to a new largest dimension.</summary>
        private static void ResizeTo(GameObject instance, float desiredSize)
        {
            var prefabSize = PropLibrary.BaseSize(instance);
            if (prefabSize <= 0.0001f) return;
            instance.transform.localScale = Vector3.one * (desiredSize / prefabSize);
        }

        /// <summary>Places an instance so its lowest point sits at the given ground point.</summary>
        private static void AlignBase(GameObject instance, Vector3 groundPoint)
        {
            instance.transform.localPosition = groundPoint;
            var bounds = PropLibrary.Measure(instance);
            float lift = groundPoint.y - bounds.min.y;
            instance.transform.localPosition = groundPoint + new Vector3(0f, lift, 0f);
        }

        private static void SetMaterial(GameObject go, Material material)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        private static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        /// <summary>Live obstacles, exposed for tests and debug overlays.</summary>
        public int LiveObstacleCount
        {
            get
            {
                int count = 0;
                foreach (var segment in _active)
                {
                    foreach (var go in segment.Content)
                    {
                        if (go != null && go.activeSelf && go.GetComponent<ObstacleMarker>() != null) count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Live coins, exposed for tests and debug overlays.</summary>
        public int LiveCoinCount
        {
            get
            {
                int count = 0;
                foreach (var pickup in _pickups)
                {
                    if (pickup != null && pickup.gameObject.activeSelf && pickup.IsCoin) count++;
                }
                return count;
            }
        }
    }
}
