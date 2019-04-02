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
    public class TestRpcClientBase : RpcClientBase
    {
        public TestRpcClientBase(
            IModel channel, 
            PublishOptions publishOptions, 
            ConsumeOptions consumeOptions, 
            FormatOptions formatOptions) : base(channel, publishOptions, consumeOptions, formatOptions)
        {
        }
    }
}
