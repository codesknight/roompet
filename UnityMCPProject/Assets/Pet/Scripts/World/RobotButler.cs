using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// 管家机器人: the smart furniture that looks after the pets for you.
    ///
    /// Click to toggle. <b>Working</b> (eyes glow green): it follows whichever pet is in the worst
    /// state and quietly services it — feeding, putting it to sleep, playing, washing, and clearing
    /// up any accident it passes. <b>Standby</b> (eyes black): it stays exactly where it was left
    /// (and can be dragged around in placement mode). It has no needs and no brain of its own; it is
    /// pure convenience, which is what the 5000-coin price buys.
    /// </summary>
    public class RobotButler : MonoBehaviour
    {
        public bool Working { get; private set; }

        private const string WorkingKey = "dshpet.robot.working";

        // Below this MoodScore the pet counts as "状态不好" and the robot goes to help.
        private const float ServiceThreshold = 0.60f;
        private const float FollowStopDistance = 1.2f;
        private const float ServiceRange = 1.7f;
        private const float CleanRange = 2.2f;
        private const float MoveSpeed = 2.4f;
        private const float ServiceInterval = 1.8f;
        private const float CleanInterval = 2.5f;

        private Transform _body;
        private Material _eyeMat;
        private Transform _alert;
        private float _nextServiceAt;
        private float _nextCleanAt;

        private static readonly Color EyeGreen = new Color(0.15f, 1f, 0.40f);
        private static readonly Color EyeOff = new Color(0.04f, 0.05f, 0.06f);

        private void Awake()
        {
            Working = PlayerPrefs.GetInt(WorkingKey, 0) == 1;
        }

        public void SetWorking(bool on)
        {
            Working = on;
            PlayerPrefs.SetInt(WorkingKey, on ? 1 : 0);
            PlayerPrefs.Save();
            UpdateEyes();
        }

        /// <summary>Builds the robot's visual (called by the room right after adding this component,
        /// so the Interactable added afterwards still finds the renderers).</summary>
        public void BuildModel()
        {
            var body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            _body = body;

            var shell = new Color(0.66f, 0.72f, 0.82f);
            var shellLight = new Color(0.80f, 0.84f, 0.92f);
            var dark = new Color(0.30f, 0.34f, 0.40f);
            var accent = new Color(0.24f, 0.55f, 0.92f);

            // Base (a low plinth / wheel skirt).
            Part(body, PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.34f, 0.07f, 0.34f), dark);
            // Torso.
            Part(body, PrimitiveType.Sphere, new Vector3(0f, 0.28f, 0f), new Vector3(0.34f, 0.34f, 0.30f), shell);
            // Head.
            Part(body, PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.24f, 0.22f, 0.24f), shellLight);
            // Face screen.
            Part(body, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.11f), new Vector3(0.15f, 0.10f, 0.02f), new Color(0.08f, 0.14f, 0.22f));
            // Antenna.
            Part(body, PrimitiveType.Cylinder, new Vector3(0f, 0.70f, 0f), new Vector3(0.02f, 0.07f, 0.02f), dark);
            Part(body, PrimitiveType.Sphere, new Vector3(0f, 0.78f, 0f), Vector3.one * 0.05f, accent);

            // A red "!" alert that pops up above the head when the pantry runs dry (feedback: 缺粮
            // 头顶冒感叹号提醒购买). Parented to the root so it never tilts with the facing.
            var alert = new GameObject("Alert");
            alert.transform.SetParent(transform, false);
            alert.transform.localPosition = new Vector3(0f, 0.90f, 0f);
            var tm = alert.AddComponent<TextMesh>();
            tm.text = "!";
            tm.fontSize = 72;
            tm.characterSize = 0.07f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.25f, 0.18f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _alert = alert.transform;
            _alert.gameObject.SetActive(false);

            // Eyes: one shared material so the on/off colour change lights both at once.
            var eyeMat = new Material(Shader.Find("Standard"));
            eyeMat.name = "Dsh_RobotEye";
            eyeMat.color = EyeOff;
            eyeMat.EnableKeyword("_EMISSION");
            eyeMat.SetColor("_EmissionColor", Color.black);
            _eyeMat = eyeMat;

            AddEye(body, new Vector3(-0.06f, 0.58f, 0.12f), eyeMat);
            AddEye(body, new Vector3(0.06f, 0.58f, 0.12f), eyeMat);

            UpdateEyes();
        }

        private void AddEye(Transform parent, Vector3 position, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Eye";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * 0.045f;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Destroy(go.GetComponent<Collider>());
        }

        private Transform Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "Part";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.6f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        private void UpdateEyes()
        {
            if (_eyeMat == null) return;
            if (Working)
            {
                _eyeMat.color = EyeGreen;
                _eyeMat.EnableKeyword("_EMISSION");
                _eyeMat.SetColor("_EmissionColor", EyeGreen * 1.8f);
            }
            else
            {
                _eyeMat.color = EyeOff;
                _eyeMat.DisableKeyword("_EMISSION");
                _eyeMat.SetColor("_EmissionColor", Color.black);
                HideAlert();
            }
        }

        private void ShowAlert()
        {
            if (_alert != null) _alert.gameObject.SetActive(true);
        }

        private void HideAlert()
        {
            if (_alert != null) _alert.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Working) return;

            var target = FindWorstPet();
            if (target != null)
            {
                MoveToward(target);
                ServicePet(target);
            }

            CleanNearbyMess();
        }

        private PetController FindWorstPet()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return null;

            PetController worst = null;
            float worstScore = float.MaxValue;

            var cards = gm.Cards();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card.Controller == null || card.Needs == null) continue;
                float score = card.Needs.MoodScore;
                if (score < worstScore)
                {
                    worstScore = score;
                    worst = card.Controller;
                }
            }

            return worstScore < ServiceThreshold ? worst : null;
        }

        private void MoveToward(PetController target)
        {
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;

            if (to.magnitude > FollowStopDistance)
            {
                Vector3 dir = to.normalized;
                transform.position += dir * MoveSpeed * Time.deltaTime;
            }

            FaceTo(to);
        }

        private void FaceTo(Vector3 dir)
        {
            if (_body == null || dir.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            _body.rotation = Quaternion.Slerp(_body.rotation, want, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        private void ServicePet(PetController target)
        {
            if (Time.time < _nextServiceAt) return;

            var needs = target != null ? target.Needs : null;
            if (needs == null) return;
            if (Vector3.Distance(transform.position, target.transform.position) > ServiceRange) return;

            string dominant = needs.DominantNeed;
            if (dominant == "饿了")
            {
                // The butler feeds from the warehouse only; an empty pantry shows the "!" and it does
                // NOT magically conjure a meal (feedback: 只用仓库粮/水喂食，不够就提醒购买).
                if (PetInventory.TryConsumeMeal())
                {
                    needs.Feed(0.35f);
                    HideAlert();
                }
                else if (PetInventory.TryConsumeWater())
                {
                    needs.GiveWater(0.3f);
                    HideAlert();
                }
                else
                {
                    ShowAlert();
                    _nextServiceAt = Time.time + ServiceInterval;
                    return;
                }
            }
            else
            {
                HideAlert();
                switch (dominant)
                {
                    case "想上厕所": needs.Relieve(); break;
                    case "困了": needs.Sleep(1.8f); break;
                    case "想玩": needs.Play(0.28f, false); break;
                    case "想洗澡": needs.Clean(0.4f); break;
                    default: needs.Pet(0.06f); break;
                }
            }

            _nextServiceAt = Time.time + ServiceInterval;
        }

        private void CleanNearbyMess()
        {
            if (Time.time < _nextCleanAt) return;

            var gm = PetGameManager.Instance;
            if (gm == null || gm.Room == null) return;

            var messes = gm.Room.Messes;
            for (int i = 0; i < messes.Count; i++)
            {
                var mess = messes[i];
                if (mess == null) continue;
                if (Vector3.Distance(transform.position, mess.transform.position) > CleanRange) continue;

                gm.Room.RemoveMess(mess);
                _nextCleanAt = Time.time + CleanInterval;
                PetHud.SetToast("管家机器人清理了地上的便便。");
                return;
            }
        }
    }
}
