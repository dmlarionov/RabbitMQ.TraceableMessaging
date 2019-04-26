using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.EventArgs;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging
{
    /// <summary>
    /// Base class for RPC server
    /// </summary>
    public abstract class RpcServerBase<TTelemetryContext, TSecurityContext> : IDisposable
        where TTelemetryContext: class, new()
        where TSecurityContext: SecurityContext, new()
    {
        /// <summary>
        /// Flag: Has Dispose already been called?
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Event handler delegate for request consuming
        /// </summary>
        public event EventHandler<RequestEventArgs<TTelemetryContext, TSecurityContext>> Received;

        /// <summary>
        /// Default timeout to stop processing remote call (milliseconds)
        /// </summary>
        public int DefaultTimeout { get; set; } = 60000;  // 1 minute

        /// <summary>
        /// RabbitMQ channel
        /// </summary>
        protected IModel _channel;

        /// <summary>
        /// Options to consume request
        /// </summary>
        protected ConsumeOptions _consumeOptions;

        /// <summary>
        /// Options, settings and delegates to deserialize / serialize
        /// </summary>
        protected FormatOptions _formatOptions;

        /// <summary>
        /// Settings and delegates for security implementation
        /// </summary>
        protected SecurityOptions<TSecurityContext> _securityOptions;

        /// <summary>
        /// Remote calls that currently are in processing.
        /// </summary>
        /// <typeparam name="string">CorrelationId of request</typeparam>
        /// <typeparam name="RemoteCall">Remote call</typeparam>
        protected ConcurrentDictionary<string, RemoteCall<TTelemetryContext, TSecurityContext>> _remoteCalls 
            = new ConcurrentDictionary<string, RemoteCall<TTelemetryContext, TSecurityContext>>();

        /// <summary>
        /// Number of current (in processing) remote calls
        /// </summary>
        public int ActiveCallsCount { get; protected set; } = 0;

        /// <summary>
        /// Creates basic server for RPC over RabbitMQ
        /// </summary>
        /// <param name="channel">RabbitMQ channel</param>
        /// <param name="consumeOptions">Options to consume request</param>
        /// <param name="formatOptions">Options, settings and delegates to deserialize / serialize</param>
        /// <param name="securityOptions">Settings and delegates for security implementation</param>
        public RpcServerBase(
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
            
            if (String.IsNullOrEmpty(formatOptions?.ContentType))
                throw new ArgumentNullException(nameof(formatOptions), "Content Type must be provided");
            
            if (formatOptions?.CreateObjectFromBytes == null ||
                formatOptions?.CreateBytesFromObject == null)
                throw new ArgumentNullException(nameof(formatOptions), "Conversion delegates object <-> bytes must be provided");
            
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
        protected bool IsSubscribed(out EventHandler<RequestEventArgs<TTelemetryContext, TSecurityContext>> handler)
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
            EventHandler<RequestEventArgs<TTelemetryContext, TSecurityContext>> handler;

            // check if we have subscriptions
            if (IsSubscribed(out handler))
            {
                // correlation id
                string correlationId;

                // request type
                string requestType;

                // remote call
                RemoteCall<TTelemetryContext, TSecurityContext> remoteCall;

                // create remote call object
                if (CreateRemoteCall(
                    ea.BasicProperties, 
                    ea.DeliveryTag, 
                    out correlationId,
                    out requestType,
                    out remoteCall))
                {
                    try
                    {
                        // check if content type and encoding is valid (if present)
                        CheckContentType(ea.BasicProperties);
                        
                        // create telemetry context (if configured)
                        remoteCall.Telemetry = CreateTelemetryContext(ea, remoteCall);

                        // should we skip all security actions for this request type?
                        if (_securityOptions?.SkipForRequestTypes == null ||
                            !_securityOptions.SkipForRequestTypes.Contains(remoteCall.RequestType))
                        {
                            // create security context (if option provided)
                            if (_securityOptions?.CreateSecurityContext != null)
                            {
                                // extract access token
                                string accessTokenEncoded;
                                object _accessToken;
                                if (remoteCall.Headers.TryGetValue("AccessToken", out _accessToken))
                                    accessTokenEncoded = Encoding.UTF8.GetString((byte[])_accessToken);
                                else
                                    throw new UnauthorizedException("No AccessToken provided");
                                
                                // create security context
                                remoteCall.Security = _securityOptions.CreateSecurityContext(accessTokenEncoded);
                            }

                            // authorize (if option provided)
                            if (_securityOptions?.Authorize != null)
                            {
                                if (remoteCall.Security != null)
                                {
                                    // authorize
                                    var authz = _securityOptions.Authorize(remoteCall.RequestType, remoteCall.Security);
                                    
                                    if (!authz.IsAuthorized)
                                        throw new ForbiddenException($"Authorization error: {authz.Error}");
                                    else
                                    {
                                        // override thread current principal
                                        Thread.CurrentPrincipal = remoteCall.Security.Principal;

                                        // update telemetry with security info
                                        if (remoteCall.Telemetry != null)
                                            UpdateTelemetryContext(
                                                remoteCall.Telemetry, 
                                                remoteCall.Security);
                                    }
                                }
                                else
                                    throw new UnauthorizedException("No Security Context");
                            }
                        }

                        // create and fire event
                        handler(this, new RequestEventArgs<TTelemetryContext, TSecurityContext>(
                            correlationId,
                            requestType,
                            ea.Body,
                            _formatOptions,
                            remoteCall.Telemetry,
                            remoteCall.Security,
                            remoteCall.Timeout));
                    }
                    catch(UnauthorizedException e)
                    {
                        TrackException(e);
                        ReplyWithUnauthorized(correlationId, remoteCall.Telemetry, e.Message);
                    }
                    catch(ForbiddenException e)
                    {
                        TrackException(e);
                        ReplyWithForbidden(correlationId, remoteCall.Telemetry, e.Message);
                    }
                    catch(Exception e)
                    {
                        TrackException(e);
                        ReplyWithFail(correlationId, remoteCall.Telemetry, e.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Check requirements (must have things for any request) and create remote call
        /// </summary>
        /// <param name="props">Delivery props</param>
        /// <param name="deliveryTag">Delivery tag</param>
        /// <param name="correlationId">Correlation Id</param>
        /// <param name="requestType">Request type</param>
        /// <param name="RemoteCall">Resulting remote call object</param>
        /// <returns>True if remote call is created</returns>
        protected virtual bool CreateRemoteCall(
            IBasicProperties props, 
            ulong deliveryTag, 
            out string correlationId,
            out string requestType,
            out RemoteCall<TTelemetryContext, TSecurityContext> remoteCall)
        {
            // reply routing key
            string replyTo;

            // request headers
            IDictionary<string, object> headers;

            // max time to process request
            int timeout;
            
            try
            {
                // extract correlationId
                if (props.IsCorrelationIdPresent())
                    correlationId = props.CorrelationId;
                else
                    throw new Exception("No CorrelationId");

                // extract ReplyTo
                if (props.IsReplyToPresent())
                    replyTo = props.ReplyTo;
                else
                    throw new Exception("No ReplyTo");

                // extract headers
                if (!props.IsHeadersPresent())
                    throw new Exception("No Headers");
                else
                    headers = props.Headers;
                
                // extract timeout
                object _timeout;
                if (headers.TryGetValue("Timeout", out _timeout))
                {
                    timeout = (int)_timeout;
                }
                else
                    timeout = DefaultTimeout;

                // extract request type
                object _requestType;
                if (headers.TryGetValue("RequestType", out _requestType))
                    requestType = Encoding.UTF8.GetString((byte[])_requestType);
                else
                    throw new Exception("No RequestType");

                // create remote call
                remoteCall = new RemoteCall<TTelemetryContext, TSecurityContext>() {
                    RequestType = requestType,
                    ReplyExchange = "",
                    ReplyRoutingKey = replyTo,
                    DeliveryTag = deliveryTag,
                    TimeoutTimer = new Timer(TerminateRemoteCall, correlationId, timeout, Timeout.Infinite),
                    Timeout = timeout,
                    Headers = headers
                };

                // keep remote call
                _remoteCalls[correlationId] = remoteCall;

                // increase counter of current calls
                ActiveCallsCount++;

                return true;
            }
            catch(Exception e)
            {
                // register exception in telemetry
                TrackException(e);

                // throw in debug
                #if DEBUG
                throw e;
                
                // stay alive in production
                #else
                correlationId = null;
                requestType = null;
                remoteCall = null;
                return false;
                
                #endif
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
        /// <param name="remoteCall">Remote call</param>
        /// <returns>Telemetry context</returns>
        protected abstract TTelemetryContext CreateTelemetryContext(
            BasicDeliverEventArgs ea,
            RemoteCall<TTelemetryContext, TSecurityContext> remoteCall);


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

        /// <summary>
        /// Send reply to calling party using RabbitMQ.
        /// </summary>
        /// <param name="correlationId">CorrelationId from request</param>
        /// <param name="reply">Reply object</param>
        /// <param name="properties">Preinitialized properties for publishing</param>
        public virtual void Reply(
            string correlationId,
            Reply reply,
            IBasicProperties properties = null) 
        {
            // check input parameters
            if (string.IsNullOrEmpty(correlationId))
                throw new ArgumentException($"{nameof(correlationId)} is null or empty");

            if (reply == null)
                throw new ArgumentNullException(nameof(reply));

            // send reply to remote call
            RemoteCall<TTelemetryContext, TSecurityContext> call;
            if (_remoteCalls.TryGetValue(correlationId, out call))
            {
                // setup properties
                var props = properties ?? _channel.CreateBasicProperties();
                props.CorrelationId = correlationId;
                props.Expiration = "10000";    // 10 sec
                props.ContentType = _formatOptions.ContentType;
                props.ContentEncoding = "utf-8";

                // serialize reply
                byte[] body;
                try
                {
                    body = _formatOptions.CreateBytesFromObject(reply);
                }
                catch (Exception e)
                {
                    // register exception in telemetry
                    TrackException(e);

                    // throw in debug
                    #if DEBUG
                    throw e;
                    
                    // stay alive in production
                    #else
                    return;
                    
                    #endif
                }

                // publish reply
                _channel.BasicPublish(
                    exchange: call.ReplyExchange,
                    routingKey: call.ReplyRoutingKey,
                    basicProperties:props,
                    body: body
                );
            }

            // terminate remote call
            TerminateRemoteCall(correlationId);
        }

        /// <summary>
        /// Termination of a remote call from System.Threading.Timer
        /// </summary>
        /// <param name="correlationId">CorrelationId of request</param>
        protected void TerminateRemoteCall(object correlationId) => TerminateRemoteCall((string) correlationId);

        /// <summary>
        /// Termination of a remote call (internal method)
        /// </summary>
        /// <param name="correlationId">CorrelationId of request</param>
        protected virtual void TerminateRemoteCall(string correlationId)
        {
            // get remote call (will succeed only once for particular correlationId)
            RemoteCall<TTelemetryContext, TSecurityContext> call;
            if (_remoteCalls.TryRemove(correlationId, out call))
            {
                // asknowlege request message if it was not done automatically upon receive
                if (!_consumeOptions.AutoAck)
                    _channel.BasicAck(deliveryTag: call.DeliveryTag, multiple: false);
            }

            // decrease counter
            ActiveCallsCount--;
        }

        /// <summary>
        /// Reply with fail status code.
        /// </summary>
        /// <param name="correlationId">CorrelationId</param>
        /// <param name="telemetry">Telemetry context</param>
        /// <param name="errorMessage">Error message</param>
        protected virtual void ReplyWithFail(
            string correlationId,
            TTelemetryContext telemetry = null,
            string errorMessage = null)
        {
            Reply(
                correlationId,
                new Reply {
                    Status = ReplyStatus.Fail,
                    ErrorMessage = errorMessage
                });
        }
        
        /// <summary>
        /// Reply with unauthorized status code. 
        /// It meant to start authorization challenge on client-side.
        /// </summary>
        /// <param name="correlationId">CorrelationId</param>
        /// <param name="telemetry">Telemetry context</param>
        /// <param name="errorMessage">Error message</param>
        protected virtual void ReplyWithUnauthorized(
            string correlationId,
            TTelemetryContext telemetry = null,
            string errorMessage = null)
        {
            Reply(
                correlationId,
                new Reply {
                    Status = ReplyStatus.Unauthorized,
                    ErrorMessage = errorMessage
                });
        }

        /// <summary>
        /// Reply with forbidden status code. 
        /// It meant to show forbidden screen on client-side.
        /// </summary>
        /// <param name="correlationId">CorrelationId</param>
        /// <param name="telemetry">Telemetry context</param>
        /// <param name="errorMessage">Error message</param>
        protected virtual void ReplyWithForbidden(
            string correlationId,
            TTelemetryContext telemetry = null,
            string errorMessage = null)
        {
            Reply(
                correlationId,
                new Reply {
                    Status = ReplyStatus.Forbidden,
                    ErrorMessage = errorMessage
                });
        }

        /// <summary>
        /// Track exception to telemetry and reply according to exception type.
        /// By using this in try .. catch you create correct reply if dependency throws exception.
        /// </summary>
        /// <param name="e"></param>
        public void Fail(RequestEventArgs<TTelemetryContext, TSecurityContext> ea, Exception e)
        {
            TrackException(e);
            if (e.GetType() == typeof(UnauthorizedException))
                ReplyWithUnauthorized(ea.CorrelationId, ea.Telemetry, e.Message);
            else if (e.GetType() == typeof(ForbiddenException))
                ReplyWithForbidden(ea.CorrelationId, ea.Telemetry, e.Message);
            else
                ReplyWithFail(ea.CorrelationId, ea.Telemetry, e.Message);
        }

        /// <summary>
        /// Disposing timeout timers
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                foreach(var call in _remoteCalls.Values)
                    call.TimeoutTimer.Dispose();
            }

            disposed = true;
        }
    }
}