using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Where the pet's brain gets its language model. Deliberately provider-agnostic: it
    /// speaks the OpenAI chat-completions shape, which DeepSeek, most gateways and most
    /// local servers all implement.
    ///
    /// Resolution order, highest priority first:
    ///   1. Values saved from the in-game settings panel (PlayerPrefs)
    ///   2. Environment variables (DEEPSEEK_*, then OPENAI_*)
    ///   3. The defaults below
    ///
    /// With no key at all the game still runs: <see cref="LocalPetBrain"/> takes over so
    /// the demo never dead-ends on a missing credential.
    /// </summary>
    [Serializable]
    public class PetBrainConfig
    {
        public const string DefaultBaseUrl = "https://api.deepseek.com";
        public const string DefaultModel = "deepseek-chat";

        public string BaseUrl = DefaultBaseUrl;
        public string Model = DefaultModel;
        public string ApiKey = "";

        [Range(0f, 2f)] public float Temperature = 1.25f;

        /// <summary>Generous on purpose: reasoning models spend most of the budget on
        /// hidden thinking tokens, and too small a cap comes back with empty content.</summary>
        [Range(64, 2048)] public int MaxTokens = 700;

        [Range(5, 120)] public int TimeoutSeconds = 30;

        /// <summary>Force the offline brain even when a key is present.</summary>
        public bool ForceOffline;

        /// <summary>Allow an endpoint that needs no Authorization header (local gateways).</summary>
        public bool AllowAnonymous;

        /// <summary>
        /// Free-text instructions appended to the generated system prompt.
        ///
        /// Deliberately additive rather than a full override: the generated prompt carries the
        /// reply format contract, the memory digest and the state block, and replacing it
        /// wholesale is a one-keystroke way to break the pet. Appending lets the player steer
        /// the tone ("说话再短一点") without that risk.
        /// </summary>
        public string ExtraInstructions = "";

        /// <summary>
        /// Full override of the generated system prompt. Empty = use the generated one. Advanced:
        /// the generated prompt carries the reply-format contract, so this is for users who want to
        /// write the whole prompt themselves.
        /// </summary>
        public string SystemPrompt = "";

        public bool HasEndpoint => !string.IsNullOrWhiteSpace(BaseUrl);

        public bool CanUseNetwork =>
            !ForceOffline &&
            HasEndpoint &&
            (AllowAnonymous || !string.IsNullOrWhiteSpace(ApiKey));

        /// <summary>Full chat-completions URL, tolerating a base URL that already ends in
        /// /v1 (or any other path).</summary>
        public string Endpoint
        {
            get
            {
                string trimmed = (BaseUrl ?? "").Trim().TrimEnd('/');
                if (trimmed.Length == 0) return "";
                if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return trimmed;
                return trimmed + "/chat/completions";
            }
        }

        public string Describe()
        {
            if (ForceOffline) return "离线模式（已强制）";
            if (!CanUseNetwork) return "离线模式（" + StatusDetail() + "）";
            return $"在线 · {Model} @ {BaseUrl}";
        }

        // ------------------------------------------------------------------- storage

        private const string Key = "dshpet.brain.config";

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static PetBrainConfig Load()
        {
            var config = new PetBrainConfig();

            // 2. environment, before the saved panel values so the panel can override.
            string key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            string baseUrl = Environment.GetEnvironmentVariable("DEEPSEEK_BASE_URL");
            string model = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL");

            if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
            if (string.IsNullOrWhiteSpace(model)) model = Environment.GetEnvironmentVariable("OPENAI_MODEL");

            if (!string.IsNullOrWhiteSpace(key)) config.ApiKey = key.Trim();
            if (!string.IsNullOrWhiteSpace(baseUrl)) config.BaseUrl = baseUrl.Trim();
            if (!string.IsNullOrWhiteSpace(model)) config.Model = model.Trim();

            // A plain http endpoint is almost always a local gateway that wants no key.
            if (config.BaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) config.AllowAnonymous = true;

            // 1. saved panel values win — but only when they actually carry information.
            // An empty saved key must not shadow a key that is present in the environment,
            // which is exactly the trap that made a working gateway look "offline".
            string saved = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<PetBrainConfig>(saved);
                    if (loaded != null)
                    {
                        if (!string.IsNullOrWhiteSpace(loaded.BaseUrl)) config.BaseUrl = loaded.BaseUrl;
                        if (!string.IsNullOrWhiteSpace(loaded.Model)) config.Model = loaded.Model;
                        if (!string.IsNullOrWhiteSpace(loaded.ApiKey)) config.ApiKey = loaded.ApiKey;

                        config.Temperature = loaded.Temperature;
                        config.MaxTokens = loaded.MaxTokens;
                        config.TimeoutSeconds = loaded.TimeoutSeconds;
                        config.ForceOffline = loaded.ForceOffline;
                        // Assigned unconditionally, unlike the fields above: for free text an
                        // empty saved value is deliberate ("the player cleared it"), not
                        // "no information". Every field added here MUST also be added to this
                        // list — a field that is saved but not read back vanishes silently,
                        // which is how ExtraInstructions was lost the first time.
                        config.ExtraInstructions = loaded.ExtraInstructions ?? "";
                        // OR, not assign: the environment may already have required it.
                        config.AllowAnonymous = config.AllowAnonymous || loaded.AllowAnonymous;
                    }
                }
                catch
                {
                    // A corrupt entry should never stop the game from starting.
                }
            }

            return config;
        }

        /// <summary>Re-resolve from the environment, ignoring the saved panel values.</summary>
        public static PetBrainConfig FromEnvironment()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            return Load();
        }

        /// <summary>Why the brain is offline, in words the player can act on.</summary>
        public string StatusDetail()
        {
            if (ForceOffline) return "已在设置里强制离线";
            if (!HasEndpoint) return "没有填接口地址";

            bool looksLocal = (BaseUrl ?? "").StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            if (looksLocal && !AllowAnonymous)
            {
                return "这是 http:// 地址，需要勾选「允许无鉴权」";
            }
            if (!AllowAnonymous && string.IsNullOrWhiteSpace(ApiKey))
            {
                return "没有 API Key —— 在「设置」里填入，或设好 DEEPSEEK_API_KEY 环境变量后重启编辑器";
            }
            return "就绪";
        }
    }
}
