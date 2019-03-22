using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Options
{
    public sealed class TrackRequestOptions
    {
        /// <summary>
        /// Returns operation name for telemetry, that must reflect what kind of logic is going to be performed by request.
        /// </summary>
        /// <param name="request">Request object</param>
        /// <param name="ea">Deliver event args of message received from RabbitMQ</param>
        /// <returns>Operation name, which is short human understandable value (low cardinality)</returns>
        public Func<BasicDeliverEventArgs, string> ExtractOperationName { get; set; } = (ea) => 
        {
            return (!string.IsNullOrEmpty(ea.Exchange)) ? 
                $"Dequeue from direct://{ea.Exchange}/{ea.RoutingKey}" : 
                $"Dequeue from direct://(default)/{ea.RoutingKey}";
        };
    }
}