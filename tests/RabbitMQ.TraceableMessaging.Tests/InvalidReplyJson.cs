using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.Tests.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests
{
    public class InvalidReplyJson : InvalidReply_Spec<JsonFormatOptions>
    {
        public InvalidReplyJson(InvalidReplyFixture<JsonFormatOptions> fixture) 
            : base(fixture)
        {
        }
    }
}
