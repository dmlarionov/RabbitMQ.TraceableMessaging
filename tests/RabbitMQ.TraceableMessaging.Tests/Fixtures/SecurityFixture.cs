using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.EventArgs;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Tests.Impl;
using RabbitMQ.TraceableMessaging.Tests.Impl.Models;
using RabbitMQ.TraceableMessaging.Tests.Impl.Options;
using RabbitMQ.TraceableMessaging.Tests.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests.Fixtures
{
    public class SecurityFixture<TFormatOptions> : PingPongFixture<TFormatOptions> where TFormatOptions : FormatOptions, new()
    {
        protected override TestRpcServerBase CreateTestRpcServerBase(
            IModel serverChannel,
            ConsumeOptions consumeOptions,
            TFormatOptions formatOptions) =>
                new TestRpcServerBase(
                    serverChannel, 
                    consumeOptions, 
                    formatOptions, 
                    new TestSecurityOptions {
                        SkipForRequestTypes = new List<string>{ "Ping2" }
                    });
    }
}
