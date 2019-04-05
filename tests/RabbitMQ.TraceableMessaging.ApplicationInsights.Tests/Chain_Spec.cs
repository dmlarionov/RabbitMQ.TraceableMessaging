using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Fixtures;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Models;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.TelemetryStubbing;
using System;
using Xunit;
using System.Threading.Tasks;
using System.Linq;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Tests
{
    public class Chain_Spec : IClassFixture<ChainFixture<JsonFormatOptions>>
    {
        ChainFixture<JsonFormatOptions> Fixture;

        TelemetryClient Telemetry;

        public Chain_Spec(ChainFixture<JsonFormatOptions> fixture)
        {
            Fixture = fixture;
            Telemetry = Stubbing.GetTelemetryClient(Fixture.TelemetryChannel);
        }

        private bool _chaincheck(string operationName)
        {
            var items = Fixture.TelemetryChannel.Items;

            var first = items.Where(i => i.Context.Operation.Name == operationName).First() as RequestTelemetry;
            var second = items.Where(i => i.Context.Operation.ParentId == first.Id).First() as DependencyTelemetry;
            var third = items.Where(i => i.Context.Operation.ParentId == second.Id).First() as RequestTelemetry;

            return third != null;
        }

        [Fact]
        public async void Chain1()
        {
            var operationName = "chain1_root_operation";
            await Task.Run(() => {
                var operation = Telemetry.StartOperation<RequestTelemetry>(operationName);
                var task = Fixture.GetReplyAsync<Pong1>(new Ping1());
                Telemetry.StopOperation(operation);
                return task;
            }).ContinueWith(task =>
            {
                Assert.True(task.Result.GetType().Name == nameof(Pong1));
                Assert.True(_chaincheck(operationName));
            });
        }

        [Fact]
        public async void Chain2()
        {
            var operationName = "chain2_root_operation";
            await Task.Run(() =>
            {
                var operation = Telemetry.StartOperation<RequestTelemetry>(operationName);
                var task = Fixture.GetReplyAsync<Pong1>(new Ping1());
                Telemetry.StopOperation(operation);
                return task;
            }).ContinueWith(task =>
            {
                var fixture = Fixture;
                Assert.ThrowsAsync<InvalidReplyException>(() => task);
                Assert.True(_chaincheck(operationName));
            });
        }

        [Fact]
        public async void BoomChain()
        {
            var operationName = "boom_chain_root_operation";
            await Task.Run(() =>
            {
                var operation = Telemetry.StartOperation<RequestTelemetry>(operationName);
                var task = Fixture.GetReplyAsync<Pong2>(new object());
                Telemetry.StopOperation(operation);
                return task;
            }).ContinueWith(task =>
            {
                var fixture = Fixture;
                Assert.ThrowsAsync<InvalidReplyException>(() => task);
                Assert.True(_chaincheck(operationName));
            });
        }
    }
}
