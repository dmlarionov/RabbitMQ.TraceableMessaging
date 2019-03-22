using System;

namespace RabbitMQ.TraceableMessaging.Exceptions
{
    /// <summary>
    /// User is identified on remote service but access forbidden
    /// </summary>
    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message) : base(message) {}
        public ForbiddenException() : base ("Forbidden") {}
    }
}