using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Tests
{
    static class Utility
    {
        public static ConnectionFactory GetConnectionFactory() =>
            new ConnectionFactory()
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                VirtualHost = "/"
            };


        public static IConnection CreateConnection() =>
            GetConnectionFactory().CreateConnection();

        public static TelemetryConfiguration GetTelemetryConfiguration(ITelemetryChannel telemetryChannel)
        {
            var configuration = new TelemetryConfiguration
            {
                TelemetryChannel = telemetryChannel,
                InstrumentationKey = Guid.NewGuid().ToString()
            };
            configuration.TelemetryInitializers.Add(new ActivityTelemetryInitializer());
            return configuration;
        }

        public static TelemetryClient GetTelemetryClient(ITelemetryChannel telemetryChannel)
            => new TelemetryClient(GetTelemetryConfiguration(telemetryChannel));
    }
}
