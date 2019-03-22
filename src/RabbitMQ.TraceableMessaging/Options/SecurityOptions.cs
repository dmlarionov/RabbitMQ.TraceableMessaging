using System;
using System.Collections.Generic;
using System.Security.Claims;
using RabbitMQ.TraceableMessaging.Models;

namespace RabbitMQ.TraceableMessaging.Options
{
    /// <summary>
    /// Settings and delegates for security implementation
    /// </summary>
    public class SecurityOptions<TSecurityContext>
        where TSecurityContext: class, new()
    {
        /// <summary>
        /// Creates security context from encoded access token (string).
        /// Throws exceptions related to token validation.
        /// </summary>
        public Func<string, TSecurityContext> CreateSecurityContext { get; set; }

        /// <summary>
        /// Creates authorization result from request type (string) and security context.
        /// </summary>
        public Func<string, TSecurityContext, AuthzResult> Authorize { get; set; }

        /// <summary>
        /// Security context and authorization is skipped for listed here request types.
        /// This is dual-use option: 
        /// 1) put early implemented request types here if service is introduced without security, but become protected later.
        /// 2) put public request types here if you have public applications (for unauthorized users).
        /// </summary>
        public ICollection<string> SkipForRequestTypes { get; set; }
    }
}