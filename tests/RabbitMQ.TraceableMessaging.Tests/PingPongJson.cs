using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.Tests.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests
{
    public class PingPongJson : PingPong_Spec<JsonFormatOptions>
    {
        public PingPongJson(PingPongFixture<JsonFormatOptions> fixture)
            : base(fixture)
        {
        }
    }
}
