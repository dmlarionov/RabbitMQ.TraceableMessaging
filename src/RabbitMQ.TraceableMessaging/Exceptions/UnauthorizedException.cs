using System;

namespace RabbitMQ.TraceableMessaging.Exceptions
{
    /// <summary>
    /// No valid access token provided or user is not identified on remote service
    /// </summary>
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message) : base(message) {}
        public UnauthorizedException() : base ("Unauthorized") {}
    }
}