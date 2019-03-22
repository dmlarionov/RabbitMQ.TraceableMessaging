using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging
{
    /// <summary>
    /// Asyncronous message publisher
    /// </summary>
    public abstract class PublisherBase
    {
        /// <summary>
        /// Default timeout for message (milliseconds)
        /// </summary>
        public int DefaultTimeout = 86400000; // 1 day

        /// <summary>
        /// RabbitMQ channel
        /// </summary>
        protected IModel _channel;

        /// <summary>
        /// Options to publish object
        /// </summary>
        protected PublishOptions _publishOptions;

        /// <summary>
        /// Options, settings and delegates to serialize
        /// </summary>
        protected FormatOptions _formatOptions;

        /// <summary>
        /// Creates asyncronous message publisher
        /// </summary>
        /// <param name="channel">RabbitMQ channel</param>
        /// <param name="publishOptions">Options to publish message</param>
        /// <param name="formatOptions">Options, settings and delegates to serialize</param>
        public PublisherBase(
            IModel channel,
            PublishOptions publishOptions,
            FormatOptions formatOptions)
        {
            // validate parameters
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            if (publishOptions?.RoutingKey == null)
                throw new ArgumentNullException(nameof(publishOptions), "RoutingKey (publishOptions.RoutingKey) can't be null");
            
            if (String.IsNullOrEmpty(formatOptions?.ContentType))
                throw new ArgumentNullException(nameof(formatOptions), "Content Type must be provided");

            if (formatOptions?.CreateBytesFromObject == null)
                throw new ArgumentNullException(nameof(formatOptions), "Conversion delegate object -> bytes must be provided");

            // save parameters
            _channel = channel;
            _publishOptions = publishOptions;
            _formatOptions = formatOptions;
        }

        /// <summary>
        /// Send message through RabbitMQ.
        /// </summary>
        /// <param name="@object">Message object</param>
        /// <param name="accessToken">Token (JWT encoded in JWE / JWS format)</param>
        /// <param name="timeout">Timeout for reply (milliseconds)</param>
        /// <param name="properties">Preinitialized properties for publishing</param>
        public virtual void Send(
            object @object,
            string accessToken = null,
            int? timeout = null,
            IBasicProperties properties = null)
        {
            // check input parameters
            if (@object == null)
                throw new ArgumentNullException(nameof(@object));

            // setup wait timeout
            var wt = (timeout != null) ? timeout.Value : DefaultTimeout;

            // setup properties
            var props = properties ?? _channel.CreateBasicProperties();
            props.Expiration = wt.ToString();
            props.ContentType = _formatOptions.ContentType;
            props.ContentEncoding = "utf-8";

            // initialize headers
            if (props.Headers == null)
                props.Headers = new Dictionary<string, object>();

            // add object type name (provide ability to detect it on the server side)
            props.Headers.Add("RequestType", @object.GetType().Name);

            // add access token if provided
            if (!string.IsNullOrEmpty(accessToken))
                props.Headers.Add("AccessToken", Encoding.UTF8.GetBytes(accessToken));

            // serialize request
            var body = _formatOptions.CreateBytesFromObject(@object);

            // publish request
            _channel.BasicPublish(
                exchange: _publishOptions.Exchange ?? "",
                routingKey: _publishOptions.RoutingKey,
                basicProperties: props,
                body: body
            );
        }

        /// <summary>
        /// Send message through RabbitMQ.
        /// </summary>
        /// <param name="object">Request object</param>
        /// <param name="accessToken">Token (JWT encoded in JWE / JWS format)</param>
        /// <param name="timeout">Timeout for reply (milliseconds)</param>
        /// <param name="properties">Preinitialized properties for publishing</param>
        public virtual async Task SendAsync(
            object @object,
            string accessToken = null,
            int? timeout = null,
            IBasicProperties properties = null)
                => await Task.Run(
                    () => Send(
                        @object,
                        accessToken,
                        timeout,
                        properties
                    ));
    }
}