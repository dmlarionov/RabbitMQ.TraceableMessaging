using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.TelemetryStubbing
{
    public static class Stubbing
    {
        public static TelemetryConfiguration GetTelemetryConfiguration(ITelemetryChannel telemetryChannel)
            => new TelemetryConfiguration
            {
                TelemetryChannel = telemetryChannel,
                InstrumentationKey = Guid.NewGuid().ToString()
            };

        public static TelemetryClient GetTelemetryClient(ITelemetryChannel telemetryChannel)
            => new TelemetryClient(GetTelemetryConfiguration(telemetryChannel));
    }
}
