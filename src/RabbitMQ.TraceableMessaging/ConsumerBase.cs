using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.EventArgs;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging
{
    /// <summary>
    /// Asyncronous message consumer
    /// </summary>
    public abstract class ConsumerBase<TTelemetryContext, TSecurityContext>
        where TTelemetryContext: class, new()
        where TSecurityContext: SecurityContext, new()
    {
        /// <summary>
        /// Event handler delegate for request consuming
        /// </summary>
        public event EventHandler<RequestEventArgsBase> Received;

        /// <summary>
        /// RabbitMQ channel
        /// </summary>
        protected IModel _channel;

        /// <summary>
        /// Options to consume request
        /// </summary>
        protected ConsumeOptions _consumeOptions;

        /// <summary>
        /// Options, settings and delegates to deserialize
        /// </summary>
        protected FormatOptions _formatOptions;

        /// <summary>
        /// Settings and delegates for security implementation
        /// </summary>
        protected SecurityOptions<TSecurityContext> _securityOptions;

        /// <summary>
        /// Creates asyncronous message receiver
        /// </summary>
        /// <param name="channel">RabbitMQ channel</param>
        /// <param name="consumeOptions">Options to consume message</param>
        /// <param name="formatOptions">Options, settings and delegates to deserialize</param>
        /// <param name="securityOptions">Settings and delegates for security implementation</param>
        public ConsumerBase(
            IModel channel,
            ConsumeOptions consumeOptions,
            FormatOptions formatOptions,
            SecurityOptions<TSecurityContext> securityOptions = null)
        {
            // validate parameters
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            if (String.IsNullOrEmpty(consumeOptions?.Queue))
                throw new ArgumentNullException(nameof(consumeOptions), "Queue name can't be empty or null");

            if(consumeOptions?.AutoAck != (bool?)true)
                throw new ArgumentException(nameof(consumeOptions), "Consuming without automatic acknowledgement is not supported");
            
            if (String.IsNullOrEmpty(formatOptions?.ContentType))
                throw new ArgumentNullException(nameof(formatOptions), "Content Type must be provided");
            
            if (formatOptions?.CreateObjectFromBytes == null)
                throw new ArgumentNullException(nameof(formatOptions), "Conversion delegate bytes -> object must be provided");
            
            if (securityOptions?.Authorize != null && securityOptions?.CreateSecurityContext == null)
                throw new ArgumentException(nameof(securityOptions), "Authorize delegate is present, but CreateSecurityContext delegate == null");
            
            // save parameters
            _channel = channel;
            _consumeOptions = consumeOptions;
            _formatOptions = formatOptions;
            _securityOptions = securityOptions;

            // start consuming
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += OnReceive;

            // start listening for request
            _channel.BasicConsume(
                queue: _consumeOptions.Queue,
                autoAck: _consumeOptions.AutoAck,
                consumer: consumer
            );
        }

        /// <summary>
        /// Check if we have subscriptions and return handler
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>True if subscriber exists</returns>
        protected bool IsSubscribed(out EventHandler<RequestEventArgsBase> handler)
        {
            handler = Received;
            if (handler != null)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Event processing
        /// </summary>
        /// <param name="model">Unused object (event source)</param>
        /// <param name="ea">Event arguments</param>
        protected virtual void OnReceive(
            object model,
            BasicDeliverEventArgs ea)
        {
            // handler to fire event
            EventHandler<RequestEventArgsBase> handler;

            // check for subscriptions, create and fire event
            if (IsSubscribed(out handler))
            {
                // properties of message
                var props = ea.BasicProperties;

                // object (request) type
                string objectType = null;

                // telemetry context
                TTelemetryContext telemetry = null;

                // security context
                TSecurityContext security = null;

                try
                {
                    // skip security checks?
                    bool skipSecurityChecks = false;

                    // check if content type and encoding is valid (if present)
                    CheckContentType(ea.BasicProperties);
                    
                    // extract headers
                    if (props.IsHeadersPresent())
                    {
                        // extract object type
                        object _objectType;
                        if (props.Headers.TryGetValue("RequestType", out _objectType))
                            objectType = Encoding.UTF8.GetString((byte[])_objectType);

                        // create telemetry context (if configured)
                        telemetry = CreateTelemetryContext(ea, props.Headers, objectType);

                        // should we skip all security actions for this request type?
                        if (_securityOptions?.SkipForRequestTypes == null ||
                            !_securityOptions.SkipForRequestTypes.Contains(objectType))
                            skipSecurityChecks = true;

                        if (skipSecurityChecks)
                        {
                            // create security context (if option provided)
                            if (_securityOptions?.CreateSecurityContext != null)
                            {
                                // extract access token
                                string accessTokenEncoded;
                                object _accessToken;
                                if (props.Headers.TryGetValue("AccessToken", out _accessToken))
                                    accessTokenEncoded = Encoding.UTF8.GetString((byte[])_accessToken);
                                else
                                    throw new UnauthorizedException("No AccessToken provided");
                                
                                // create security context
                                security = _securityOptions.CreateSecurityContext(accessTokenEncoded);
                            }
                        }
                    }

                    // authorize (if option provided)
                    if (!skipSecurityChecks && _securityOptions?.Authorize != null)
                    {
                        if (security != null)
                        {
                            // authorize
                            var authz = _securityOptions.Authorize(objectType, security);
                            
                            if (!authz.IsAuthorized)
                                throw new ForbiddenException($"Authorization error: {authz.Error}");
                            else
                            {
                                // override thread current principal
                                Thread.CurrentPrincipal = security.Principal;

                                // update telemetry with security info
                                if (telemetry != null)
                                    UpdateTelemetryContext(
                                        telemetry, 
                                        security);
                            }
                        }
                        else
                            throw new UnauthorizedException("No Security Context");
                    }
                    
                    // fire event
                    handler(this, new RequestEventArgsBase(objectType, ea.Body, _formatOptions));
                }
                catch(Exception e)
                {
                    TrackException(e);
                }
            }
        }

        /// <summary>
        /// Check context type and encoding (if present in header)
        /// </summary>
        protected virtual void CheckContentType(IBasicProperties props)
        {
            // check content type
            if (!props.IsContentTypePresent() ||
                props.ContentType != _formatOptions.ContentType)
                throw new Exception($"ContentType != {_formatOptions.ContentType}");

            // check encoding (if present)
            if (props.IsContentEncodingPresent() &&
                props.ContentEncoding.ToLower() != "utf-8")
                throw new Exception("ContentEncoding != utf-8");
        }

        /// <summary>
        /// Create telemetry context for remote call (if configured)
        /// </summary>
        /// <param name="ea">BasicDeliverEventArgs of request</param>
        /// <param name="headers">Headers of request</param>
        /// <param name="objectType">Message object</param>
        /// <returns>Telemetry context</returns>
        protected abstract TTelemetryContext CreateTelemetryContext(
            BasicDeliverEventArgs ea,
            IDictionary<string, object> headers,
            string objectType);

        /// <summary>
        /// Update telemetry context with security info
        /// (add principal info, token issuer and so on)
        /// </summary>
        /// <param name="telemetry">Telemetry context</param>
        /// <param name="security">Security context</param>
        protected abstract void UpdateTelemetryContext(
            TTelemetryContext telemetry,
            TSecurityContext security);

        /// <summary>
        /// Register exception in telemetry
        /// </summary>
        /// <param name="e">Exception</param>
        protected abstract void TrackException(Exception e);
    }
}