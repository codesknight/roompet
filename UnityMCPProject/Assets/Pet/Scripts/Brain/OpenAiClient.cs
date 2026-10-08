using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DshPet
{
    /// <summary>One pet brain. Coroutine based so it works on every Unity scripting
    /// backend without configuring async/await.</summary>
    public interface IPetBrain
    {
        string Name { get; }
        bool IsNetwork { get; }
        IEnumerator Think(PetContext context, string userMessage, Action<PetReply> onDone);
    }

    /// <summary>
    /// Minimal OpenAI-compatible chat client. DeepSeek, most hosted gateways and most
    /// local servers expose this exact shape, so one client covers all of them.
    /// </summary>
    public static class OpenAiClient
    {
        [Serializable]
        private class Message
        {
            public string role;
            public string content;

            public Message(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }

        [Serializable]
        private class Request
        {
            public string model;
            public Message[] messages;
            public float temperature;
            public int max_tokens;
            public bool stream;
        }

        [Serializable]
        private class Choice
        {
            public Message message;
            public string finish_reason;
        }

        [Serializable]
        private class ErrorBody
        {
            public string message;
            public string type;
        }

        [Serializable]
        private class Response
        {
            public Choice[] choices;
            public ErrorBody error;
        }

        /// <summary>Sends one turn. <paramref name="onDone"/> receives (text, error);
        /// exactly one of them is non-empty.</summary>
        public static IEnumerator Chat(
            PetBrainConfig config,
            string systemPrompt,
            IList<ChatMessage> history,
            string userMessage,
            Action<string, string> onDone)
        {
            if (config == null || !config.CanUseNetwork)
            {
                onDone?.Invoke(null, "brain not configured for network use");
                yield break;
            }

            var messages = new List<Message> { new Message("system", systemPrompt) };
            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    var turn = history[i];
                    messages.Add(new Message(turn.IsUser ? "user" : "assistant", turn.Text));
                }
            }
            messages.Add(new Message("user", userMessage ?? ""));

            var body = new Request
            {
                model = config.Model,
                messages = messages.ToArray(),
                temperature = config.Temperature,
                max_tokens = config.MaxTokens,
                stream = false
            };

            string json = JsonUtility.ToJson(body);
            byte[] payload = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(config.Endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + config.ApiKey.Trim());
                }
                request.timeout = Mathf.Max(5, config.TimeoutSeconds);

                yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif
                string text = request.downloadHandler != null ? request.downloadHandler.text : "";

                if (failed)
                {
                    string detail = string.IsNullOrEmpty(text) ? request.error : Shorten(text, 220);
                    onDone?.Invoke(null, $"HTTP {(long)request.responseCode}: {detail}");
                    yield break;
                }

                string content = ExtractContent(text, out string parseError);
                if (content == null)
                {
                    onDone?.Invoke(null, parseError ?? "empty response");
                    yield break;
                }

                onDone?.Invoke(content, null);
            }
        }

        private static string ExtractContent(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "empty body";
                return null;
            }

            Response parsed;
            try { parsed = JsonUtility.FromJson<Response>(json); }
            catch (Exception e)
            {
                error = "bad json: " + e.Message;
                return null;
            }

            if (parsed == null)
            {
                error = "bad json";
                return null;
            }

            if (parsed.error != null && !string.IsNullOrEmpty(parsed.error.message))
            {
                error = parsed.error.message;
                return null;
            }

            if (parsed.choices == null || parsed.choices.Length == 0 || parsed.choices[0].message == null)
            {
                error = "no choices in response";
                return null;
            }

            return parsed.choices[0].message.content;
        }

        private static string Shorten(string value, int max)
            => value.Length <= max ? value : value.Substring(0, max) + "…";
    }
}
