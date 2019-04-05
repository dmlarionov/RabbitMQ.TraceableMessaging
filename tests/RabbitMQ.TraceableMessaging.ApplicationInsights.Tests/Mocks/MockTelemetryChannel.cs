using Microsoft.ApplicationInsights.Channel;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Mocks
{
    public class MockTelemetryChannel : ITelemetryChannel
    {
        public IList<ITelemetry> Items
        {
            get;
            private set;
        } = new List<ITelemetry>();

        public bool? DeveloperMode { get => null; set { } }

        public string EndpointAddress { get => throw new NotImplementedException(); set { } }

        public void Dispose()
        {
        }

        public void Flush()
        {
        }

        public void Send(ITelemetry item)
        {
            Items.Add(item);
        }
    }
}
