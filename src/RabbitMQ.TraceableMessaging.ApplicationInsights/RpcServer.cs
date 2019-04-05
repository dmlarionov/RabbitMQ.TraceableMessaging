using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Models;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.EventArgs;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using RabbitMQ.TraceableMessaging.Exceptions;
using TelemetryContext = RabbitMQ.TraceableMessaging.ApplicationInsights.Models.TelemetryContext;
using System.Security.Claims;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights
{
    /// <summary>
    /// RPC server with Application Insights telemetry
    /// </summary>
    public sealed class RpcServer<TSecurityContext> : RpcServerBase<TelemetryContext, TSecurityContext>
            where TSecurityContext : SecurityContext, new()
    {
        /// <summary>
        /// Options for tracking requests
        /// </summary>
        public TelemetryOptions TelemetryOptions = new TelemetryOptions();

        /// <summary>
        /// Application Insights telemetry client
        /// </summary>
        private TelemetryClient _telemetryClient;

        /// <summary>
        /// Creates RPC server
        /// </summary>
        /// <param name="channel">RabbitMQ channel</param>
        /// <param name="consumeOptions">Options to consume request</param>
        /// <param name="formatOptions">Options to serialize / deserialize</param>
        /// <param name="securityOptions">Settings and delegates for security implementation</param>
        /// <param name="telemetryClient">Application Insights telemetry client</param>
        public RpcServer(
            IModel channel,
            ConsumeOptions consumeOptions,
            FormatOptions formatOptions,
            SecurityOptions<TSecurityContext> securityOptions = null,
            TelemetryClient telemetryClient = null) : base (channel, consumeOptions, formatOptions, securityOptions)
        {
            // save telemetry client
            _telemetryClient = telemetryClient ?? new TelemetryClient(TelemetryConfiguration.Active);
        }

        /// <summary>
        /// Create telemetry context for remote call (if configured)
        /// </summary>
        /// <param name="ea">BasicDeliverEventArgs of request</param>
        /// <param name="remoteCall">Remote call</param>
        /// <returns>Telemetry context</returns>
        protected override TelemetryContext CreateTelemetryContext(
            BasicDeliverEventArgs ea,
            RemoteCall<TelemetryContext, TSecurityContext> remoteCall)
        {
            // extract telemetry operation id
            object _operationId;
            string operationId = null;
            if (remoteCall.Headers.TryGetValue("TelemetryOperationId", out _operationId))
                operationId = Encoding.UTF8.GetString((byte[])_operationId);
            
            // extract telemetry parent operation id
            object _parentId;
            string parentId = null;
            if (remoteCall.Headers.TryGetValue("TelemetryOperationParentId", out _parentId))
                parentId = Encoding.UTF8.GetString((byte[])_parentId);

            // extract telemetry source
            object _source;
            string source = null;
            if (remoteCall.Headers.TryGetValue("TelemetrySource", out _source))
                source = Encoding.UTF8.GetString((byte[])_source);

            // start telemetry operation
            if (_telemetryClient.IsEnabled() &&
                (operationId != null || parentId != null))
            {
                Activity activity = null;
                IOperationHolder<RequestTelemetry> operation;

                if (!string.IsNullOrEmpty(parentId))
                {

                    // start diagnostic activity
                    activity = new Activity(TelemetryOptions.GetRequestName(ea));
                    activity.SetParentId(parentId);

                    // start telemetry operation (based on parent)
                    operation = _telemetryClient.StartOperation<RequestTelemetry>(activity);
                }
                else
                    // start telemetry operation (without parent)
                    operation = _telemetryClient.StartOperation<RequestTelemetry>(TelemetryOptions.GetRequestName(ea), operationId);

                // support source here (currently not supported by RpcClient)
                if (!string.IsNullOrEmpty(source))
                    operation.Telemetry.Source = source;

                // add metrics to telemetry operation
                operation.Telemetry.Metrics.Add("activeCallsCount", ActiveCallsCount);

                // create telemetry context
                return new TelemetryContext {
                    Activity = activity,
                    Operation = operation
                };
            }
            else
                return null;
        }

        /// <summary>
        /// Get telemetry context updated from security context 
        /// (with added user info, token issuer and so on)
        /// </summary>
        /// <param name="telemetry">Initial telemetry context</param>
        /// <param name="security">Security context</param>
        protected override void UpdateTelemetryContext(
            TelemetryContext telemetry,
            TSecurityContext security)
        {
            // add security properties to telemetry operation
            if (_telemetryClient.IsEnabled() && telemetry != null)
            {
                if (security.AccessTokenIssuer != null)
                    telemetry.Operation.Telemetry.Properties.Add("AccessTokenIssuer", security.AccessTokenIssuer);

                if (security.Principal?.Identity?.Name != null)
                    telemetry.Operation.Telemetry.Properties.Add("Principal.Identity.Name", security.Principal.Identity.Name);
            }
        }

        /// <summary>
        /// Reply with fail status code.
        /// </summary>
        /// <param name="correlationId">CorrelationId</param>
        /// <param name="telemetry">Telemetry context</param>
        /// <param name="errorMessage">Error message</param>
        protected override void ReplyWithFail(
            string correlationId,
            TelemetryContext telemetry = null,
            string errorMessage = null)
        {
            if (_telemetryClient.IsEnabled() && telemetry != null)
            {
                telemetry.Operation.Telemetry.Success = false;
                telemetry.Operation.Telemetry.ResponseCode = "Fail";
            }
            Reply(
                correlationId,
                new Reply {
                    Status = ReplyStatus.Fail,
                    ErrorMessage = errorMessage
                });
        }

        /// Reply with unauthorized status code. 
        /// It meant to start authorization challenge on client-side.
        /// </summary>
        /// <param name="correlationId">CorrelationId</param>
        /// <param name="telemetry">Telemetry context</param>
        /// <param name="errorMessage">Error message</param>
        protected override void ReplyWithUnauthorized(
            string correlationId,
            TelemetryContext telemetry = null,
            string errorMessage = null)
        {
            if (_telemetryClient.IsEnabled() && telemetry != null)
            {
                telemetry.Operation.Telemetry.Success = false;
                telemetry.Operation.Telemetry.ResponseCode = "Unauthorized";
            }
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
        protected override void ReplyWithForbidden(
            string correlationId,
            TelemetryContext telemetry = null,
            string errorMessage = null)
        {
            if (_telemetryClient.IsEnabled() && telemetry != null)
            {
                telemetry.Operation.Telemetry.Success = false;
                telemetry.Operation.Telemetry.ResponseCode = "Forbidden";
            }
            Reply(
                correlationId,
                new Reply {
                    Status = ReplyStatus.Forbidden,
                    ErrorMessage = errorMessage
                });
        }

        /// <summary>
        /// Method to register exceptions in telemetry
        /// </summary>
        /// <param name="e">Exception</param>
        protected override void TrackException(Exception e)
        { 
            if (_telemetryClient.IsEnabled()) 
                _telemetryClient.TrackException(e);
        }

        /// <summary>
        /// Termination of a remote call (internal method)
        /// </summary>
        /// <param name="correlationId">CorrelationId of request</param>
        protected override void TerminateRemoteCall(string correlationId)
        {
            // get remote call (will succeed only once for particular correlationId)
            RemoteCall<TelemetryContext, TSecurityContext> call;
            if (_remoteCalls.TryRemove(correlationId, out call))
            {
                // asknowlege request message if it was not done automatically upon receive
                if (!_consumeOptions.AutoAck)
                    _channel.BasicAck(deliveryTag: call.DeliveryTag, multiple: false);

                if (_telemetryClient.IsEnabled())
                {
                    // stop telemetry operation
                    var operation = call.Telemetry.Operation;
                    if (operation != null)
                        _telemetryClient.StopOperation(operation);
                }

                // decrease counter
                ActiveCallsCount--;
            }
        }
    }
}