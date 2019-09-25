using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Models;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights
{
    /// <summary>
    /// Asyncronous message consumer with Application Insights telemetry
    /// </summary>
    public class Consumer<TSecurityContext> : ConsumerBase<TelemetryContext, TSecurityContext>
        where TSecurityContext : SecurityContext, new()
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
            SecurityOptions<TSecurityContext> securityOptions = null,
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
                { "Queue", _consumeOptions.Queue },
                { "RoutingKey", ea.RoutingKey }
            };

            if (_telemetryClient.IsEnabled())
                    _telemetryClient.TrackEvent("Message consumed from RabbitMQ (not RPC)", 
                        telemetryProps);

            // context is defined for sake of transmitting properties to exception with telemetry context
            // no telemetry operation or activity is defined for non RPC scenario
            return new TelemetryContext {
                Properties = telemetryProps
            };
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

        /// <summary>
        /// Register exception in telemetry with telemetry context
        /// </summary>
        /// <param name="e">Exception</param>
        /// <param name="telemetry">Telemetry context</param>
        protected override void TrackException(Exception e, TelemetryContext telemetry)
        {
            if (_telemetryClient.IsEnabled()) 
                _telemetryClient.TrackException(e, telemetry?.Properties);
        }

        protected override void UpdateTelemetryContext(TelemetryContext telemetry, TSecurityContext security)
        {
            // nothing to do because we don't have telemetry operation
            // it is not operation it is just a message (no dependency call)
        }
    }
}