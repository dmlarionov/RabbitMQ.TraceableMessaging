using System;

namespace RabbitMQ.TraceableMessaging.Exceptions
{
    /// <summary>
    /// General failure status returned by remote service
    /// </summary>
    public class RequestFailureException : Exception
    {
        public RequestFailureException(string message) : base(message) {}
        public RequestFailureException() : base ("Request failure") {}
    }
}