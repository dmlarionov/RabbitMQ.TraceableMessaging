using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.EventArgs;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Tests.Impl;
using RabbitMQ.TraceableMessaging.Tests.Impl.Models;
using RabbitMQ.TraceableMessaging.Tests.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RabbitMQ.TraceableMessaging.Tests.Fixtures
{
    public class PingPongFixture<TFormatOptions> : IDisposable where TFormatOptions : FormatOptions, new()
    {
        public IConnection ServerConnection { get; private set; }
        public IModel ServerChannel { get; private set; }
        public TestRpcServerBase Server { get; private set; }

        public IConnection ClientConnection { get; private set; }
        public IModel ClientChannel { get; private set; }
        public TestRpcClientBase Client { get; private set; }

        public int SleepTimeout { get; set; } = 0;

        public PingPongFixture()
        {
            var connFactory = new ConnectionFactory() {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                VirtualHost = "/"
            };
            var requestQueueName = Guid.NewGuid().ToString();
            var replyQueueName = Guid.NewGuid().ToString();

            {
                // configure connection & channel for RPC server
                ServerConnection = connFactory.CreateConnection();
                ServerChannel = ServerConnection.CreateModel();

                // declare request queue
                ServerChannel.QueueDeclare(requestQueueName, false, true, true);

                // configure RPC server
                var consumeOptions = new ConsumeOptions();
                consumeOptions.Queue = requestQueueName;

                Server = new TestRpcServerBase(ServerChannel, consumeOptions, new TFormatOptions());
            }

            {
                // configure connection & channel for RPC client
                ClientConnection = connFactory.CreateConnection();
                ClientChannel = ServerConnection.CreateModel();

                // declare reply queue
                ClientChannel.QueueDeclare(replyQueueName, false, true, true);

                // configure RPC client
                var publishOptions = new PublishOptions();
                publishOptions.Exchange = "";
                publishOptions.RoutingKey = requestQueueName;

                var consumeOptions = new ConsumeOptions();
                consumeOptions.Queue = replyQueueName;

                Client = new TestRpcClientBase(ServerChannel, publishOptions, consumeOptions, new TFormatOptions());
            }

            Server.Received += Pong;
        }

        void Pong(object sender, RequestEventArgs<TestTelemetryContext, TestSecurityContext> ea)
        {
            Thread.Sleep(this.SleepTimeout);

            switch (ea.RequestType)
            {
                case nameof(Ping1):
                    Server.Reply(ea.CorrelationId, new Pong1());
                    break;

                case nameof(Ping2):
                    Server.Reply(ea.CorrelationId, new Pong2(ea.GetRequest<Ping2>().Payload));
                    break;

                default:
                    Server.Reply(ea.CorrelationId, new Reply { Status = ReplyStatus.Fail });
                    break;
            }
        }

        public void Dispose()
        {
            Server.Received -= Pong;
            ServerConnection.Dispose();
            ClientConnection.Dispose();
        }
    }
}
