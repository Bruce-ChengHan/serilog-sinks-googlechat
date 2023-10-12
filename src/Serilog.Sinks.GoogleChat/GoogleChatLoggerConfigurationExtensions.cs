using System;
using System.Collections.Generic;
using System.Net.Http;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.GoogleChat;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog
{
    public static class GoogleChatLoggerConfigurationExtensions
    {
        public static LoggerConfiguration GoogleChat(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            string outputTemplate,
            IReadOnlyCollection<string> webhooks,
            string threadKey = null,
            HttpClient httpClient = null,
            IFormatProvider formatProvider = null,
            LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
        {
            var logEventSink = CreateGoogleChat(outputTemplate, webhooks, threadKey: threadKey, httpClient: httpClient, formatProvider: formatProvider);
            return loggerSinkConfiguration.Sink(logEventSink, restrictedToMinimumLevel);
        }

        private static ILogEventSink CreateGoogleChat(string outputTemplate, IReadOnlyCollection<string> webhooks, string threadKey, HttpClient httpClient, IFormatProvider formatProvider)
        {
            if (httpClient is null)
                httpClient = new HttpClient();
            var textFormatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
            var sink = new GoogleChatSink(webhooks, threadKey, textFormatter, httpClient);
            var sinkOptions = new PeriodicBatchingSinkOptions { BatchSizeLimit = 1, Period = TimeSpan.FromSeconds(1), QueueLimit = 1000 };
            var logEventSink = new PeriodicBatchingSink(sink, sinkOptions);
            return logEventSink;
        }
    }
}