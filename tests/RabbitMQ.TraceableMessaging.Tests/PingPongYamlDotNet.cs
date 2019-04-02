using RabbitMQ.TraceableMessaging.YamlDotNet.Options;
using RabbitMQ.TraceableMessaging.Tests.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests
{
    public class PingPongYamlDotNet : PingPong_Spec<YamlFormatOptions>
    {
        public PingPongYamlDotNet(PingPongFixture<YamlFormatOptions> fixture)
            : base(fixture)
        {
        }
    }
}
