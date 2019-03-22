using System;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Options
{
    public sealed class TrackDependencyOptions
    {
        /// <summary>
        /// Returns operation name, which can be function name on the other party side, name of the other party or queue name (as last resort).
        /// </summary>
        /// <param name="publishOptions">Publish options used in request publishing to RabbitMQ</param>
        /// <param name="request">Request object</param>
        /// <returns>Operation name, which is short human understandable value (low cardinality)</returns>
        public Func<PublishOptions, object, string> ExtractOperationName { get; set; } = (PublishOptions publishOptions, object request) =>
        {
            return (!string.IsNullOrEmpty(publishOptions.Exchange)) ? 
                $"Enqueue to direct://{publishOptions.Exchange}/{publishOptions.RoutingKey}" : 
                $"Enqueue to direct://(default)/{publishOptions.RoutingKey}";
        };

        /// <summary>
        /// Returns dependency data, which is detailed information (in whatever format), defining what should be performed on the other party side.
        /// </summary>
        /// <param name="publishOptions">Publish options used in request publishing to RabbitMQ</param>
        /// <param name="request">Request object</param>
        /// <returns>Dependency data, which is detailed call information.</returns>
        public Func<PublishOptions, object, string> ExtractDependencyData { get; set; } = (PublishOptions publishOptions, object request) =>
        {
            return null;
        };

    }
}