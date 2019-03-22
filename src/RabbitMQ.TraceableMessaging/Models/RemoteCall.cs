using System.Collections.Generic;
using System.Threading;

namespace RabbitMQ.TraceableMessaging.Models
{
    /// <summary>
    /// Information associated with RPC request over RabbitMQ
    /// </summary>
    public class RemoteCall<TTelemetryContext, TSecurityContext> 
        where TTelemetryContext: class, new()
        where TSecurityContext: class, new()
    {
        /// <summary>
        /// Request type
        /// </summary>
        public string RequestType { get; set; }

        /// <summary>
        /// Exchange to reply to the call
        /// </summary>
        public string ReplyExchange { get; set; } = "";

        /// <summary>
        /// Routing key to reply to the call
        /// </summary>
        public string ReplyRoutingKey { get; set; }

        /// <summary>
        /// Delivery tag to asknowlege request (in no AutoAsk case)
        /// </summary>
        public ulong DeliveryTag { get; set; }

        /// <summary>
        /// Timer to stop processing
        /// </summary>
        public Timer TimeoutTimer { get; set; }

        /// <summary>
        /// Headers of request
        /// </summary>
        public IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Telemetry context for request
        /// </summary>
        public TTelemetryContext Telemetry { get; set; }

        /// <summary>
        /// Security context for request
        /// </summary>
        public TSecurityContext Security { get; set; }
    };
}