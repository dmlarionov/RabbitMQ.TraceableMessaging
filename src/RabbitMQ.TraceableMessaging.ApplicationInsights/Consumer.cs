using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Models;
using RabbitMQ.TraceableMessaging.Jwt.Models;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights
{
    /// <summary>
    /// Asyncronous message consumer with Application Insights telemetry
    /// </summary>
    public class Consumer : ConsumerBase<TelemetryContext, JwtSecurityContext>
    {
        /// <summary>
        /// Application Insights telemetry client
        /// </summary>
        private TelemetryClient _telemetryClient;

        /// <summary>
        /// Creates asyncronous message consumer
        /// </summary>
        /// <param name="channel">RabbitMQ channel</param>
        /// <param name="consumeOptions">Options to consume request</param>
        /// <param name="formatOptions">Options to serialize / deserialize</param>
        /// <param name="securityOptions">Settings and delegates for security implementation</param>
        /// <param name="telemetryClient">Application Insights telemetry client</param>
        public Consumer(
            IModel channel, 
            ConsumeOptions consumeOptions, 
            FormatOptions formatOptions, 
            SecurityOptions<JwtSecurityContext> securityOptions = null,
            TelemetryClient telemetryClient = null) 
                : base(channel, consumeOptions, formatOptions, securityOptions)
        {
            // save telemetry client
            _telemetryClient = telemetryClient ?? new TelemetryClient(TelemetryConfiguration.Active);
        }

        /// <summary>
        /// Create telemetry context for remote call (if configured)
        /// </summary>
        /// <param name="ea">BasicDeliverEventArgs of request</param>
        /// <param name="headers">Headers of request</param>
        /// <param name="objectType">Message object</param>
        /// <returns>Telemetry context</returns>
        protected override TelemetryContext CreateTelemetryContext(
            BasicDeliverEventArgs ea, 
            IDictionary<string, object> headers,
            string objectType)
        {
            var telemetryProps = new Dictionary<string, string>{
                { "Object Type", objectType },
                { "Queue", _consumeOptions.Queue }
            };

            if (_telemetryClient.IsEnabled())
                    _telemetryClient.TrackEvent("Message consumed from RabbitMQ (not RPC)", 
                        telemetryProps);

            // telemetry context has nothing to do with unidirectional message 
            return null;
        }

        /// <summary>
        /// Register exception in telemetry
        /// </summary>
        /// <param name="e">Exception</param>
        protected override void TrackException(Exception e)
        {
            if (_telemetryClient.IsEnabled()) 
                _telemetryClient.TrackException(e);
        }

        protected override void UpdateTelemetryContext(TelemetryContext telemetry, JwtSecurityContext security)
        {
            // nothing to do
        }
    }
}