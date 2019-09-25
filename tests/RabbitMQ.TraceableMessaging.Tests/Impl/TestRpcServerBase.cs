using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Tests.Impl.Models;
using RabbitMQ.TraceableMessaging.Tests.Impl.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests.Impl
{
    public class TestRpcServerBase : RpcServerBase<TestTelemetryContext, TestSecurityContext>
    {
        public Action OnCreateTelemetryContext;
        public Action OnTrackException;
        public Action OnUpdateTelemetryContext;

        public TestRpcServerBase(
            IModel channel,
            ConsumeOptions consumeOptions,
            FormatOptions formatOptions,
            TestSecurityOptions securityOptions = null) : base (channel, consumeOptions, formatOptions, securityOptions)
        { }

        protected override TestTelemetryContext CreateTelemetryContext(BasicDeliverEventArgs ea, RemoteCall<TestTelemetryContext, TestSecurityContext> remoteCall)
        {
            OnCreateTelemetryContext?.Invoke();
            return new TestTelemetryContext();
        }

        protected override void TrackException(Exception e)
        {
            OnTrackException?.Invoke();
        }

        protected override void TrackException(Exception e, TestTelemetryContext telemetry)
        {
            OnTrackException?.Invoke();
        }

        protected override void UpdateTelemetryContext(TestTelemetryContext telemetry, TestSecurityContext security)
        {
            OnUpdateTelemetryContext?.Invoke();
        }
    }
}
