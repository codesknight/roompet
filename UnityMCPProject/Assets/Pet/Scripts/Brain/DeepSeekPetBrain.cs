using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The online brain: builds a persona prompt from the pet's species, memory and live
    /// needs, asks an OpenAI-compatible endpoint, and parses the answer back into speech
    /// plus an action the avatar can play.
    ///
    /// Contract: <c>context.History</c> must NOT already contain
    /// <paramref name="userMessage"/> — the message is appended by the client so the
    /// model sees exactly one copy of the latest turn.
    /// </summary>
    public class DeepSeekPetBrain : IPetBrain
    {
        private readonly PetBrainConfig _config;

        public DeepSeekPetBrain(PetBrainConfig config)
        {
            _config = config ?? new PetBrainConfig();
        }

        public string Name => "在线 · " + _config.Model;
        public bool IsNetwork => true;

        public IEnumerator Think(PetContext context, string userMessage, Action<PetReply> onDone)
        {
            string systemPrompt = PetPrompting.BuildSystemPrompt(context);

            var history = new List<ChatMessage>();
            if (context.History != null) history.AddRange(context.History);

            string reply = null;
            string error = null;

            yield return OpenAiClient.Chat(_config, systemPrompt, history, userMessage,
                (text, err) => { reply = text; error = err; });

            // A reasoning model can burn the whole budget on hidden tokens and return an
            // empty content string. One retry with a bigger budget fixes that cheaply.
            if (string.IsNullOrEmpty(error) && string.IsNullOrWhiteSpace(reply))
            {
                var retryConfig = CloneWithBiggerBudget(_config);
                yield return OpenAiClient.Chat(retryConfig, systemPrompt, history, userMessage,
                    (text, err) => { reply = text; error = err; });

                if (string.IsNullOrEmpty(error) && string.IsNullOrWhiteSpace(reply))
                {
                    onDone?.Invoke(PetReply.Invalid("模型返回了空内容（推理 token 可能吃掉了预算）"));
                    yield break;
                }
            }

            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(reply))
            {
                onDone?.Invoke(PetReply.Invalid(error ?? "no reply"));
                yield break;
            }

            var parsed = PetPrompting.Parse(reply, true);
            if (!parsed.IsValid)
            {
                onDone?.Invoke(PetReply.Invalid(reply));
                yield break;
            }

            onDone?.Invoke(parsed);
        }

        private static PetBrainConfig CloneWithBiggerBudget(PetBrainConfig source)
        {
            return new PetBrainConfig
            {
                BaseUrl = source.BaseUrl,
                Model = source.Model,
                ApiKey = source.ApiKey,
                Temperature = source.Temperature,
                MaxTokens = Mathf.Min(2048, Mathf.Max(source.MaxTokens * 2, 1200)),
                TimeoutSeconds = source.TimeoutSeconds,
                ForceOffline = source.ForceOffline,
                AllowAnonymous = source.AllowAnonymous
            };
        }
    }
}
