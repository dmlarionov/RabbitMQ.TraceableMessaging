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
        public async Task Timeout1()
        {
            Assert.True(Fixture.Server.ActiveCallsCount == 0, "ActiveCallsCount expected to be 0 at start");
            var t = Fixture.Client.GetReplyAsync<Pong1>(new Ping1(), timeout: 200);
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            Assert.True(Fixture.Server.ActiveCallsCount == 1, "ActiveCallsCount expected to be 1 during request");
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            Assert.True(Fixture.Server.ActiveCallsCount == 0, "ActiveCallsCount expected to be 0 after timeout");
            t.Wait();
            Assert.IsType<TimeoutException>(t.Exception);
        }
    }
}
