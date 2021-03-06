﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Options;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights
{
    /// <summary>
    /// RPC client with Application Insights telemetry
    /// </summary>
    public sealed class RpcClient : RabbitMQ.TraceableMessaging.RpcClientBase
    {
        /// <summary>
        /// Options for tracking dependency
        /// </summary>
        public TelemetryOptions TelemetryOptions = new TelemetryOptions();

        /// <summary>
        /// Application Insights telemetry client
        /// </summary>
        private TelemetryClient _telemetryClient;

        /// <summary>
        /// Creates client for RPC over RabbitMQ with Application Insights distributed tracing
        /// </summary>
        /// <param name="channel">RabbitMQ channel</param>
        /// <param name="publishOptions">Options to publish request</param>
        /// <param name="consumeOptions">Options to consume reply</param>
        /// <param name="formatOptions">Options to serialize / deserialize</param>
        /// <param name="telemetryClient">Application Insights telemetry client</param>
        public RpcClient(
            IModel channel,
            PublishOptions publishOptions,
            ConsumeOptions consumeOptions,
            FormatOptions formatOptions,
            TelemetryClient telemetryClient = null) : base(channel, publishOptions, consumeOptions, formatOptions)
        {
            // save telemetry client
            _telemetryClient = telemetryClient ?? new TelemetryClient(TelemetryConfiguration.Active);
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
        public override TReply GetReply<TReply>(
            object request,
            string accessToken = null,
            int? timeout = null, 
            IBasicProperties properties = null)
        {
            // this variable keeps result to return
            TReply response;

            // create telemetry properties
            var telemetryProps = new Dictionary<string, string>{
                { "Object Type", request.GetType().Name },
                { "Exchange", _publishOptions.Exchange },
                { "RoutingKey", _publishOptions.RoutingKey }
            };

            // start diagnostic activity
            var activity = new Activity(TelemetryOptions.GetDependencyName(_publishOptions, request));

            // create telemetry operation
            using (var operation = _telemetryClient.StartOperation<DependencyTelemetry>(activity))
            {
                // setup telemetry operation type
                operation.Telemetry.Type = TelemetryOptions.TelemetryType;

                // enrich telemetry with properties
                if (operation.Telemetry.Properties != null)
                    foreach (var k in telemetryProps.Keys)
                        operation.Telemetry.Properties.Add(k, telemetryProps[k]);

                // try execute dependency call
                try
                {
                    // setup headers to bind telemetry on receiving side
                    var props = properties ?? _channel.CreateBasicProperties();

                    if (props.Headers == null)
                        props.Headers = new Dictionary<string, object>();

                    props.Headers.Add("TelemetryOperationId", operation.Telemetry.Context.Operation.Id);
                    props.Headers.Add("TelemetryOperationParentId", operation.Telemetry.Id);
                    //props.Headers.Add("TelemetrySource", ...); still not supported here
                    response = base.GetReply<TReply>(request, accessToken, timeout, props);

                    // set operation.Telemetry success
                    operation.Telemetry.Success = (response.Status == ReplyStatus.Success);
                }
                catch (Exception e)
                {
                    // FIXME: exception doesn't keep telemetry operation in it's context!
                    if (_telemetryClient.IsEnabled())
                        _telemetryClient.TrackException(e, operation.Telemetry?.Properties ?? telemetryProps);

                    // request is unsuccessful now
                    operation.Telemetry.Success = false;

                    // exception type
                    var t = e.GetType();

                    // assign result code to request in telemetry
                    if (t == typeof(InvalidReplyException))
                        operation.Telemetry.ResultCode = "InvalidReply";
                    else if (t == typeof(RequestFailureException))
                        operation.Telemetry.ResultCode = "Fail";
                    else if (t == typeof(UnauthorizedException))
                        operation.Telemetry.ResultCode = "Unauthorized";
                    else if (t == typeof(ForbiddenException))
                        operation.Telemetry.ResultCode = "Forbidden";
                    else if (t == typeof(TimeoutException))
                        operation.Telemetry.ResultCode = "Timeout";
                    else
                        operation.Telemetry.ResultCode = "Exception";

                    throw e;
                }
                finally
                {
                    _telemetryClient.StopOperation(operation);
                }
            }

            return response;
        }
    }
}
