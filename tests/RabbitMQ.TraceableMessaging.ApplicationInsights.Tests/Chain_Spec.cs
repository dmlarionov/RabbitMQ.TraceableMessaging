using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Fixtures;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Models;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Mocks;
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
            Telemetry = Utility.GetTelemetryClient(Fixture.TelemetryChannel);
        }

        private bool _chaincheck1(string operationName)
        {
            var items = Fixture.TelemetryChannel.Items;

            var first = items.Where(i => i.Context.Operation.Name == operationName).First() as RequestTelemetry;
            var second = items.Where(i => i.Context.Operation.ParentId == first.Id && 
                i.Context.Operation.Id == first.Context.Operation.Id).First() as DependencyTelemetry;
            var third = items.Where(i => i.Context.Operation.ParentId == second.Id &&
                i.Context.Operation.Id == first.Context.Operation.Id).First() as RequestTelemetry;

            return third != null && items.Where(i => i.Context.Operation.Id == first.Context.Operation.Id).Count() == 3;
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
                Assert.True(_chaincheck1(operationName));
            });
        }

        // FIXME: something wrong with fact that exception_at_second linked to first.Id
        // FIXME: pass when run alone, but fail when run whole spec
        private bool _chaincheck2(string operationName)
        {
            var items = Fixture.TelemetryChannel.Items;

            var first = items.Where(i => i.Context.Operation.Name == operationName).First() as RequestTelemetry;
            var second = items.Where(i => i.Context.Operation.ParentId == first.Id &&
                i.Context.Operation.Id == first.Context.Operation.Id &&
                i.GetType().Name == "DependencyTelemetry").First() as DependencyTelemetry;
            var exception_at_second = items.Where(i => i.Context.Operation.ParentId == first.Id &&
                i.Context.Operation.Id == first.Context.Operation.Id &&
                i.GetType().Name == "ExceptionTelemetry").First() as ExceptionTelemetry;
            var third = items.Where(i => i.Context.Operation.ParentId == second.Id &&
                i.Context.Operation.Id == first.Context.Operation.Id).First() as RequestTelemetry;

            return third != null && items.Where(i => i.Context.Operation.Id == first.Context.Operation.Id).Count() == 4;
        }

        [Fact]
        public async void Chain2()
        {
            var operationName = "chain2_root_operation";
            await Task.Run(() =>
            {
                var operation = Telemetry.StartOperation<RequestTelemetry>(operationName);
                var task = Fixture.GetReplyAsync<Pong2>(new Ping2());
                Telemetry.StopOperation(operation);
                return task;
            }).ContinueWith(task =>
            {
                var fixture = Fixture;
                Assert.ThrowsAsync<InvalidReplyException>(() => task);
                Assert.True(_chaincheck2(operationName));
            });
        }

        // FIXME: something wrong with fact that exception_at_second linked to first.Id
        // and exception_at_third linked to second.Id
        private bool _boomchaincheck(string operationName)
        {
            var items = Fixture.TelemetryChannel.Items;

            var first = items.Where(i => i.Context.Operation.Name == operationName).First() as RequestTelemetry;
            var second = items.Where(i => i.Context.Operation.ParentId == first.Id &&
                i.Context.Operation.Id == first.Context.Operation.Id && 
                i.GetType().Name == "DependencyTelemetry").First() as DependencyTelemetry;
            var exception_at_second = items.Where(i => i.Context.Operation.ParentId == first.Id &&
                i.Context.Operation.Id == first.Context.Operation.Id &&
                i.GetType().Name == "ExceptionTelemetry").First() as ExceptionTelemetry;
            var third = items.Where(i => i.Context.Operation.ParentId == second.Id &&
                i.Context.Operation.Id == first.Context.Operation.Id &&
                i.GetType().Name == "RequestTelemetry").First() as RequestTelemetry;
            var exception_at_third = items.Where(i => i.Context.Operation.ParentId == second.Id &&
                i.Context.Operation.Id == first.Context.Operation.Id &&
                i.GetType().Name == "ExceptionTelemetry").First() as ExceptionTelemetry;

            return third != null && items.Where(i => i.Context.Operation.Id == first.Context.Operation.Id).Count() == 5;
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
                Assert.True(_boomchaincheck(operationName));
            });
        }
    }
}
