using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.EventArgs;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Tests.Impl;
using RabbitMQ.TraceableMessaging.Tests.Impl.Models;
using RabbitMQ.TraceableMessaging.Tests.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests.Fixtures
{
    public class InvalidReplyFixture<TFormatOptions> : IDisposable where TFormatOptions : FormatOptions, new()
    {
        public IConnection Connection { get; private set; }

        public IModel ServerChannel { get; private set; }
        public EventingBasicConsumer Consumer { get; private set; }

        public IModel ClientChannel { get; private set; }
        public TestRpcClientBase Client { get; private set; }

        public InvalidReplyFixture()
        {
            Connection = Utility.CreateConnection();
            var requestQueueName = Guid.NewGuid().ToString();
            var replyQueueName = Guid.NewGuid().ToString();

            {
                // configure connection & channel for "RPC server"
                ServerChannel = Connection.CreateModel();

                // declare request queue
                ServerChannel.QueueDeclare(requestQueueName, false, true, true);

                // start consuming (without RPC server)
                Consumer = new EventingBasicConsumer(ServerChannel);
                Consumer.Received += Pong;

                // start listening for request
                ServerChannel.BasicConsume(
                    queue: requestQueueName,
                    autoAck: true,
                    consumer: Consumer
                );
            }

            {
                // configure connection & channel for RPC client
                ClientChannel = Connection.CreateModel();

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
        }

        void Pong(object sender, BasicDeliverEventArgs ea)
        {
            // get request type
            object _requestType;
            string requestType = null;
            if (ea.BasicProperties.Headers.TryGetValue("RequestType", out _requestType))
                requestType = Encoding.UTF8.GetString((byte[])_requestType);

            // setup reply properties
            var props = ServerChannel.CreateBasicProperties();
            props.CorrelationId = ea.BasicProperties.CorrelationId;

            var formatOptions = new TFormatOptions();

            switch (requestType)
            {
                case "Ping1":
                    props.ContentType = "wrong/type";
                    props.ContentEncoding = "utf-8";
                    break;

                case "Ping2":
                    props.ContentType = formatOptions.ContentType;
                    props.ContentEncoding = "wrong-encoding";
                    break;

                case "default":
                    props.ContentType = formatOptions.ContentType;
                    props.ContentEncoding = "utf-8";
                    break;
            }

            // serialize reply
            byte[] body = (new TFormatOptions()).CreateBytesFromObject(new object());

            // publish reply
            ServerChannel.BasicPublish(
                exchange: "",
                routingKey: ea.BasicProperties.ReplyTo,
                basicProperties: props,
                body: body
            );
        }

        public void Dispose()
        {
            Consumer.Received -= Pong;
            Connection.Dispose();
        }
    }
}
