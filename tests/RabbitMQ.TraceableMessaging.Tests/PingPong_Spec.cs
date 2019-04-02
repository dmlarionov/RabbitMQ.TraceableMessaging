using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Tests.Fixtures;
using RabbitMQ.TraceableMessaging.Tests.Models;
using System;
using Xunit;

namespace RabbitMQ.TraceableMessaging.Tests
{
    public abstract class PingPong_Spec<TFormatOptions> : IClassFixture<PingPongFixture<TFormatOptions>> where TFormatOptions : FormatOptions, new()
    {
        PingPongFixture<TFormatOptions> Fixture;

        public PingPong_Spec(PingPongFixture<TFormatOptions> fixture)
        {
            Fixture = fixture;
        }

        [Fact]
        public void PingPong1()
        {
            var r = Fixture.Client.GetReply<Pong1>(new Ping1());
            Assert.True(r.GetType().Name == nameof(Pong1));
        }

        [Fact]
        public void PingPong2()
        {
            var r = Fixture.Client.GetReply<Pong2>(new Ping2("valueX"));
            Assert.True(r.Payload == "valueX");
        }

        [Fact]
        public void UnknownPing()
        {
            Assert.Throws<RequestFailureException>(() => Fixture.Client.GetReply<Pong2>(new object()));
        }
    }
}
