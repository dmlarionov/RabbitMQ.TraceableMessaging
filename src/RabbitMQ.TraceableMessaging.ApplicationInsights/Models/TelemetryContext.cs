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
    }
}