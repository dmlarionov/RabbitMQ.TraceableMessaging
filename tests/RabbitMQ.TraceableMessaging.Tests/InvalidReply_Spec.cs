using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Tests.Fixtures;
using RabbitMQ.TraceableMessaging.Tests.Models;
using System;
using Xunit;

namespace RabbitMQ.TraceableMessaging.Tests
{
    public abstract class InvalidReply_Spec<TFormatOptions> : IClassFixture<InvalidReplyFixture<TFormatOptions>> where TFormatOptions : FormatOptions, new()
    {
        InvalidReplyFixture<TFormatOptions> Fixture;

        public InvalidReply_Spec(InvalidReplyFixture<TFormatOptions> fixture)
        {
            Fixture = fixture;
        }

        [Fact]
        public void WrongType()
        {
            Assert.Throws<InvalidReplyException>(() => Fixture.Client.GetReply<Pong1>(new Ping1()));
        }

        [Fact]
        public void WrongEncoding()
        {
            Assert.Throws<InvalidReplyException>(() => Fixture.Client.GetReply<Pong2>(new Ping2()));
        }
    }
}
