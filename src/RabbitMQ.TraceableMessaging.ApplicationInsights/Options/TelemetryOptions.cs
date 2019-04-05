using System;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Options
{
    public sealed class TelemetryOptions
    {
        /// <summary>
        /// Telemetry type name
        /// </summary>
        public string TelemetryType { get; set; }  = "RabbitMQ";

        /// <summary>
        /// Operation name for RabbitMQ dependency call
        /// </summary>
        /// <param name="publishOptions">Publish options used in request publishing to RabbitMQ</param>
        /// <param name="request">Request object</param>
        /// <returns>Operation name, which is low cardinality human understandable value</returns>
        public Func<PublishOptions, object, string> GetDependencyName { get; set; } = (PublishOptions publishOptions, object request) =>
        {
            return (!string.IsNullOrEmpty(publishOptions.Exchange)) ? 
                $"Dependency call rabbitmq://{publishOptions.RoutingKey} using exchange '{publishOptions.Exchange}'" : 
                $"Dependency call rabbitmq://{publishOptions.RoutingKey}";
        };

        /// <summary>
        /// Operation name for RabbitMQ incoming request.
        /// </summary>
        /// <param name="request">Request object</param>
        /// <param name="ea">Event args of message received from RabbitMQ</param>
        /// <returns>Operation name, which is low cardinality human understandable value</returns>
        public Func<BasicDeliverEventArgs, string> GetRequestName { get; set; } = (ea) =>
        {
            return (!string.IsNullOrEmpty(ea.Exchange)) ?
                $"Request from rabbitmq://{ea.RoutingKey} using exchange '{ea.Exchange}'" :
                $"Request from rabbitmq://{ea.RoutingKey}";
        };
    }
}