using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging
{
    /// <summary>
    /// Base class for RPC client
    /// </summary>
    public abstract class RpcClientBase
    {
        /// <summary>
        /// Default timeout for request-reply cycle (milliseconds)
        /// </summary>
        public int DefaultTimeout = 60000;

        /// <summary>
        /// RabbitMQ channel
        /// </summary>
        protected IModel _channel;

        /// <summary>
        /// Options to publish request
        /// </summary>
        protected PublishOptions _publishOptions;

        /// <summary>
        /// Options to consume reply
        /// </summary>
        protected ConsumeOptions _consumeOptions;

        /// <summary>
        /// Options to serialize / deserialize
        /// </summary>
        protected FormatOptions _formatOptions;

        /// <summary>
        /// Reset events, used to let waiting GetReply<T>(...) thread to continue
        /// </summary>
        /// <typeparam name="string">correlationId</typeparam>
        /// <typeparam name="ManualResetEventSlim">Reset event</typeparam>
        protected ConcurrentDictionary<string, ManualResetEventSlim> resetEvents = new ConcurrentDictionary<string, ManualResetEventSlim>();
       
        /// <summary>
        /// Response event args, transfered to GetReply<T>(...) thread
        /// </summary>
        /// <typeparam name="string">correlationId</typeparam>
        /// <typeparam name="BasicDeliverEventArgs">Event args</typeparam>
        protected ConcurrentDictionary<string, BasicDeliverEventArgs> replyEAs = new ConcurrentDictionary<string, BasicDeliverEventArgs>();

        /// <summary>
        /// Creates basic client for RPC over RabbitMQ
        /// </summary>
        /// <param name="channel">RabbitMQ channel</param>
        /// <param name="publishOptions">Options to publish request</param>
        /// <param name="consumeOptions">Options to consume reply</param>
        /// <param name="formatOptions">Options, settings and delegates to deserialize / serialize</param>
        public RpcClientBase(
            IModel channel,
            PublishOptions publishOptions,
            ConsumeOptions consumeOptions,
            FormatOptions formatOptions)
        {
            // validate parameters
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));
            
            // validate parameters
            if (String.IsNullOrEmpty(consumeOptions?.Queue))
                throw new ArgumentNullException(nameof(consumeOptions), "Response queue name (consumeOptions.Queue) can't be empty or null");

            if (String.IsNullOrEmpty(publishOptions?.RoutingKey))
                throw new ArgumentNullException(nameof(publishOptions), "RoutingKey (publishOptions.RoutingKey) can't be empty or null");

            if (String.IsNullOrEmpty(formatOptions?.ContentType))
                throw new ArgumentNullException(nameof(formatOptions), "Content Type must be provided");

            if (formatOptions?.CreateBytesFromObject == null)
                throw new ArgumentNullException(nameof(formatOptions), "Conversion delegate object -> bytes must be provided");

            // save parameters
            _channel = channel;
            _publishOptions = publishOptions;
            _consumeOptions = consumeOptions;
            _formatOptions = formatOptions;

            // start consuming
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                if (ea.BasicProperties.IsCorrelationIdPresent())
                {
                    // correlationId
                    var correlationId = ea.BasicProperties.CorrelationId;
                    
                    // check if we are waiting for reply message by correlationId
                    if(resetEvents.ContainsKey(correlationId))
                    {
                        // put reply message into dictionary (it's going to be read by thread of GetReply<T>(...))
                        replyEAs[correlationId] = ea;
                        // let waiting GetReply<T>(...) thread to continue
                        resetEvents[correlationId].Set();
                    }
                }

                // asknowlege message if it was not done automatically upon receive
                if (!consumeOptions.AutoAck)
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            // start listening for reply
            _channel.BasicConsume(
                queue: _consumeOptions.Queue,
                autoAck: _consumeOptions.AutoAck,
                consumer: consumer
            );
        }

        /// <summary>
        /// Get reply from service through RabbitMQ.
        /// </summary>
        /// <param name="request">Request object</param>
        /// <param name="accessToken">Token (JWT encoded in JWE / JWS format)</param>
        /// <param name="timeout">Timeout for reply (milliseconds)</param>
        /// <param name="properties">Preinitialized properties for publishing</param>
        /// <typeparam name="TReply">Reply object type</typeparam>
        /// <returns>Reply object</returns>
        public virtual TReply GetReply<TReply>(
            object request,
            string accessToken = null,
            int? timeout = null,
            IBasicProperties properties = null) where TReply : Reply, new()
        {
            // check input parameters
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // setup wait timeout
            var wt = (timeout != null) ? timeout.Value : DefaultTimeout;

            // create reset event for new correlationId
            var correlationId = Guid.NewGuid().ToString();
            var resetEvent = new ManualResetEventSlim();
            resetEvents[correlationId] = resetEvent;

            // setup properties
            var props = properties ?? _channel.CreateBasicProperties();
            props.ReplyTo = _consumeOptions.Queue;
            props.CorrelationId = correlationId;
            props.Expiration = wt.ToString();
            props.ContentType = _formatOptions.ContentType;
            props.ContentEncoding = "utf-8";

            // initialize headers
            if (props.Headers == null)
                props.Headers = new Dictionary<string, object>();
            
            // add wait timeout to the header (to prevent handling request on the server side longer than reply is awaited)
            props.Headers.Add("Timeout", wt);

            // add request type name (provide ability to detect it on the server side)
            props.Headers.Add("RequestType", request.GetType().Name);

            // add access token if provided
            if (!string.IsNullOrEmpty(accessToken))
                props.Headers.Add("AccessToken", Encoding.UTF8.GetBytes(accessToken));

            // serialize request
            var body = _formatOptions.CreateBytesFromObject(request);

            // publish request
            _channel.BasicPublish(
                exchange: _publishOptions.Exchange,
                routingKey: _publishOptions.RoutingKey,
                basicProperties: props,
                body: body
            );

            try
            {
                if(resetEvent.Wait(wt))
                {
                    // if reply did arrive in time
                    var ea = replyEAs[correlationId];

                    // check content type
                    if (!ea.BasicProperties.IsContentTypePresent() ||
                            ea.BasicProperties.ContentType != _formatOptions.ContentType)
                        throw new InvalidReplyException($"Reply ContentType != {_formatOptions.ContentType}");
                    
                    // check encoding (if present)
                    if (ea.BasicProperties.IsContentEncodingPresent() &&
                            ea.BasicProperties.ContentEncoding.ToLower() != "utf-8")
                        throw new InvalidReplyException("Reply ContentEncoding != utf-8");

                    // get result
                    var reply = (TReply)(_formatOptions.CreateObjectFromBytes(ea.Body, typeof(TReply)));

                    // translate status codes to exceptions
                    switch(reply.Status)
                    {
                        case ReplyStatus.Fail:
                            throw new RequestFailureException($"Remote call failure: {reply.ErrorMessage}");

                        case ReplyStatus.Unauthorized:
                            throw new UnauthorizedException($"Unauthorized for remote call: {reply.ErrorMessage}");

                        case ReplyStatus.Forbidden:
                            throw new ForbiddenException($"Forbidden access to remote service: {reply.ErrorMessage}");

                        default:
                            return reply;
                    }
                }
                else
                {
                    throw new TimeoutException($"Reply didn't arrive in {wt} milliseconds");
                }
            }
            finally
            {
                // we don't wait for reply with this correlationId anymore
                replyEAs.TryRemove(correlationId, out _);
                resetEvents.TryRemove(correlationId, out _);
            }
        }

        /// <summary>
        /// Get reply from service through RabbitMQ.
        /// </summary>
        /// <param name="request">Request object</param>
        /// <param name="accessToken">Token (JWT encoded in JWE / JWS format)</param>
        /// <param name="timeout">Timeout for reply (milliseconds)</param>
        /// <param name="properties">Preinitialized properties for publishing</param>
        /// <typeparam name="TReply">Reply object type</typeparam>
        /// <returns>Reply object</returns>
        public virtual async Task<TReply> GetReplyAsync<TReply>(
            object request,
            string accessToken = null,
            int? timeout = null,
            IBasicProperties properties = null) where TReply : Reply, new()
                => await Task.Run<TReply>(
                    () => GetReply<TReply>(
                        request,
                        accessToken,
                        timeout,
                        properties
                    ));
    }
}
