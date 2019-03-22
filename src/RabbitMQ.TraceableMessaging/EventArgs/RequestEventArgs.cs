using System;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.EventArgs
{
    public class RequestEventArgs<TTelemetryContext, TSecurityContext> : RequestEventArgsBase
        where TTelemetryContext: class, new()
        where TSecurityContext: class, new()
    {
        /// <summary>
        /// Correlation Id
        /// </summary>
        public string CorrelationId { get; private set; }

        /// <summary>
        /// Telemetry context for request
        /// </summary>
        public TTelemetryContext Telemetry { get; set; }

        /// <summary>
        /// Security context for request
        /// </summary>
        public TSecurityContext Security { get; set; }
        
        /// <summary>
        /// Constructor of RequestEventArgs
        /// </summary>
        public RequestEventArgs(
            string correlationId,
            string requestType,
            byte[] body,
            FormatOptions formatOptions,
            TTelemetryContext telemetry = null,
            TSecurityContext security = null)
                : base(requestType, body, formatOptions)
        {
            if (string.IsNullOrEmpty(correlationId))
                throw new ArgumentNullException(nameof(correlationId));
            else
                CorrelationId = correlationId;
            
            Telemetry = telemetry;
            Security = security;
        }
    }
}