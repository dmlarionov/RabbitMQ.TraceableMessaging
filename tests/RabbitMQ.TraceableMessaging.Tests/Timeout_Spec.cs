using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.Tests.Fixtures;
using RabbitMQ.TraceableMessaging.Tests.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RabbitMQ.TraceableMessaging.Tests
{
    public class Timeout_Spec : IClassFixture<PingPongFixture<JsonFormatOptions>>
    {
        PingPongFixture<JsonFormatOptions> Fixture;

        public Timeout_Spec(PingPongFixture<JsonFormatOptions> fixture)
        {
            Fixture = fixture;
            Fixture.SleepTimeout = 100;
        }

        [Fact]
        public async Task Timeout()
        {
            await Assert.ThrowsAsync<TimeoutException>(() => Fixture.Client.GetReplyAsync<Pong1>(new Ping1(), timeout: 50));
        }

        [Fact]
        public async Task NoTimeout()
        {
            await Fixture.Client.GetReplyAsync<Pong1>(new Ping1(), timeout: 250);
        }
    }
}
