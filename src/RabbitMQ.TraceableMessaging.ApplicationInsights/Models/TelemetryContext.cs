using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.TraceableMessaging.Models;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Models
{
    public sealed class TelemetryContext
    {
        public Activity Activity { get; set; }
        public IOperationHolder<RequestTelemetry> Operation { get; internal set; }

        /// <summary>
        /// Telemetry properties defined just after message is consumed (to use in exceptions)
        /// </summary>
        public IDictionary<string, string> Properties { get; set; }
    }
}