using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights
{
    /// <summary>
    /// Asyncronous message publisher with Application Insights telemetry
    /// </summary>
    public class Publisher : PublisherBase
    {
        /// <summary>
        /// Application Insights telemetry client
        /// </summary>
        private TelemetryClient _telemetryClient;

        /// <summary>
        /// Creates asyncronous message publisher
        /// </summary>
        /// <param name="channel">RabbitMQ channel</param>
        /// <param name="publishOptions">Options to publish message</param>
        /// <param name="formatOptions">Options, settings and delegates to serialize</param>
        public Publisher(
            IModel channel, 
            PublishOptions publishOptions, 
            FormatOptions formatOptions,
            TelemetryClient telemetryClient = null) 
                : base(channel, publishOptions, formatOptions)
        {
            // save telemetry client
            _telemetryClient = telemetryClient ?? new TelemetryClient(TelemetryConfiguration.Active);
        }

        /// <summary>
        /// Send message through RabbitMQ.
        /// </summary>
        /// <param name="@object">Message object</param>
        /// <param name="accessToken">Token (JWT encoded in JWE / JWS format)</param>
        /// <param name="timeout">Timeout for reply (milliseconds)</param>
        /// <param name="properties">Preinitialized properties for publishing</param>
        public override void Send(
            object @object,
            string accessToken = null,
            int? timeout = null,
            IBasicProperties properties = null)
        {
            var telemetryProps = new Dictionary<string, string>{
                { "Object Type", @object.GetType().Name },
                { "Exchange", _publishOptions.Exchange },
                { "RoutingKey", _publishOptions.RoutingKey }
            };
            try
            {
                base.Send(@object, accessToken, timeout, properties);
                if (_telemetryClient.IsEnabled())
                    _telemetryClient.TrackEvent("Message published to RabbitMQ (not RPC)", 
                        telemetryProps);
            }
            catch (Exception e)
            {
                if (_telemetryClient.IsEnabled())
                    _telemetryClient.TrackException(e, telemetryProps);
            }
        }
    }
}