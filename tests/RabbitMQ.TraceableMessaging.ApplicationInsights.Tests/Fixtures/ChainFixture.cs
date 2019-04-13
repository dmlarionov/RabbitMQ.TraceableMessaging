using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.EventArgs;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Models;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Impl;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Impl.Models;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Models;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Mocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Fixtures
{
    public class ChainFixture<TFormatOptions> : IDisposable where TFormatOptions : FormatOptions, new()
    {
        IConnection Connection { get; set; } = Utility.CreateConnection();

        public MockTelemetryChannel TelemetryChannel { get; private set; } = new MockTelemetryChannel();

        public Item Root { get; private set; }

        /// <summary>
        /// Each chain item listens to server queue and requests for next chain item
        /// </summary>
        public class Item
        {
            public TelemetryClient TelemetryClient { get; set; }

            public IConnection Connection { get; private set; }

            public IModel ServerChannel { get; private set; }
            public RpcServer<TestSecurityContext> Server { get; private set; }

            public IModel DependencyChannel { get; private set; }
            public RpcClient DependencyClient { get; private set; }

            public string Queue { get; private set; }
            public Item Dependency { get; set; }

            /// <summary>
            /// Create chain item
            /// </summary>
            /// <param name="telemetryChannel">Telemetry channel</param>
            /// <param name="queue">Queue to listen</param>
            /// <param name="dependency">Item to forward request</param>
            public Item(IConnection connection, MockTelemetryChannel telemetryChannel, string queue, Item dependency = null)
            {
                if (string.IsNullOrEmpty(queue))
                    throw new ArgumentNullException(nameof(queue));
                Queue = queue;

                Dependency = dependency;

                Connection = connection ?? throw new ArgumentNullException(nameof(connection));

                // configure telemetry client
                TelemetryClient = Utility.GetTelemetryClient(telemetryChannel);

                {
                    // configure connection & channel for RPC server
                    ServerChannel = Connection.CreateModel();

                    // declare request queue
                    ServerChannel.QueueDeclare(queue, false, true, true);

                    // configure RPC server
                    var consumeOptions = new ConsumeOptions();
                    consumeOptions.Queue = queue;

                    Server = new RpcServer<TestSecurityContext>(
                        ServerChannel,
                        consumeOptions,
                        new TFormatOptions(),
                        null,
                        TelemetryClient);
                }

                if (dependency != null)
                {
                    var replyQueueName = Guid.NewGuid().ToString();

                    // configure connection & channel for RPC client
                    DependencyChannel = Connection.CreateModel();

                    // declare reply queue
                    DependencyChannel.QueueDeclare(replyQueueName, false, true, true);

                    // configure RPC client
                    var publishOptions = new PublishOptions(dependency.Queue);

                    var consumeOptions = new ConsumeOptions(replyQueueName);

                    DependencyClient = new RpcClient(
                        ServerChannel,
                        publishOptions,
                        consumeOptions,
                        new TFormatOptions(),
                        TelemetryClient);
                }

                Server.Received += ForwardOrPong;
            }

            void ForwardOrPong(object sender, RequestEventArgs<TelemetryContext, TestSecurityContext> ea)
            {
                try
                {
                    switch (ea.RequestType)
                    {
                        // Ping1 chain always success in the end
                        case nameof(Ping1):
                            if (DependencyClient != null)
                                Server.Reply(ea.CorrelationId,
                                    DependencyClient.GetReply<Pong1>(
                                        ea.GetRequest<Ping1>(),
                                        ea.Security.AccessTokenEncoded,
                                        ea.Timeout));
                            else
                                Server.Reply(ea.CorrelationId, new Pong1());
                            break;

                        // Ping2 chain always fail in the end
                        case nameof(Ping2):
                            if (DependencyClient != null)
                                Server.Reply(ea.CorrelationId,
                                    DependencyClient.GetReply<Pong2>(
                                        ea.GetRequest<Ping2>(),
                                        ea.Security.AccessTokenEncoded,
                                        ea.Timeout));
                            else
                                Server.Reply(ea.CorrelationId, new Reply { Status = ReplyStatus.Fail });
                            break;

                        // any other chain explores in the end
                        default:
                            throw new Exception("boom!");
                    }
                }
                catch(Exception e)
                {
                    // track exceptions on server side
                    TelemetryClient.TrackException(e);
                    Server.Reply(ea.CorrelationId, new Reply { Status = ReplyStatus.Fail });
                }
            }

            public Task<TReply> GetReplyAsync<TReply>(
            object request,
            string accessToken = null,
            int? timeout = null,
            IBasicProperties properties = null) where TReply : Reply, new()
                => DependencyClient.GetReplyAsync<TReply>(
                    request,
                    accessToken,
                    timeout,
                    properties);

            public void Dispose()
            {
                Server.Received -= ForwardOrPong;
                Dependency?.Dispose();
            }
        }

        public ChainFixture()
        {
            Item item = null;
            for (int i = 0; i < 2; i++)
                item = new Item(Connection, TelemetryChannel, Guid.NewGuid().ToString(), item);
            Root = item;
        }

        public void FlushTelemetry()
        {
            var item = Root;
            while(item != null)
            {
                item.TelemetryClient.Flush();
                item = item.Dependency;
            }
        }

        public Task<TReply> GetReplyAsync<TReply>(
            object request,
            string accessToken = null,
            int? timeout = null,
            IBasicProperties properties = null) where TReply : Reply, new()
                => Root.GetReplyAsync<TReply>(
                    request,
                    accessToken,
                    timeout,
                    properties);

        public void Dispose() => Root.Dispose();
    }
}
