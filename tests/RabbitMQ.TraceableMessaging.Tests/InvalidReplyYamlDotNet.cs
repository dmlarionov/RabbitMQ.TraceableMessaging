using RabbitMQ.TraceableMessaging.YamlDotNet.Options;
using RabbitMQ.TraceableMessaging.Tests.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests
{
    public class InvalidReplyYamlDotNet : InvalidReply_Spec<YamlFormatOptions>
    {
        public InvalidReplyYamlDotNet(InvalidReplyFixture<YamlFormatOptions> fixture)
            : base(fixture)
        {
        }
    }
}
