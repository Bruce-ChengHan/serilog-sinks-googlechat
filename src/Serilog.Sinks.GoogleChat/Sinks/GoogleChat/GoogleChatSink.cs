using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.GoogleChat.Sinks.GoogleChat
{
    public class GoogleChatSink : IBatchedLogEventSink, IDisposable
    {
        private const int StringBuilderCapacity = 256;
        private readonly IReadOnlyCollection<string> _webhooks;
        private readonly ITextFormatter _textFormatter;
        private readonly string _threadKey;
        private readonly HttpClient _httpClient;

        public GoogleChatSink(IReadOnlyCollection<string> webhooks, ITextFormatter textFormatter, string threadKey, HttpClient httpClient)
        {
            _webhooks = webhooks;
            _textFormatter = textFormatter;
            _threadKey = threadKey;
            _httpClient = httpClient;
        }

        public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
        {
            var notifyTasks = new List<Task>();

            foreach (var logEvent in batch)
            {
                var message = FormatMessage(logEvent);

                foreach (var webhook in _webhooks)
                {
                    notifyTasks.Add(NotifyAsync(webhook, message));
                }
            }

            await Task.WhenAll(notifyTasks);
        }

        private async Task NotifyAsync(string webhook, string message)
        {
            var data = new
            {
                text = message
            };
            var jsonPayload = JsonConvert.SerializeObject(data);
            using (var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
            {
                var threadKeyContent = string.IsNullOrWhiteSpace(_threadKey) ? "" : $"&threadKey={_threadKey}&messageReplyOption=REPLY_MESSAGE_FALLBACK_TO_NEW_THREAD";
                var url = $"{webhook}{threadKeyContent}";
                using (var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.OK) { }
                    else if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new LoggingFailedException($"此 webhook 已失效 : {webhook}");
                    }
                    else
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        throw new LoggingFailedException($"發送 Google Chat 時發生錯誤，HttpStatusCode : {response.StatusCode}，body: {body}");
                    }
                }
            }
        }

        private string FormatMessage(LogEvent logEvent)
        {
            var buffer = new StringWriter(new StringBuilder(StringBuilderCapacity));

            _textFormatter.Format(logEvent, buffer);

            return buffer.ToString();
        }

        public Task OnEmptyBatchAsync() => Task.CompletedTask;

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}