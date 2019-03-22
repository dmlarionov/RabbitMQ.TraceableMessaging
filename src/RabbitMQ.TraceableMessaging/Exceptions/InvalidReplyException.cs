using System;

namespace RabbitMQ.TraceableMessaging.Exceptions
{
    /// <summary>
    /// Ivalid reply from remote procedure (wrong content type or encoding)
    /// </summary>
    public class InvalidReplyException : Exception
    {
        public InvalidReplyException(string message) : base(message) {}
        public InvalidReplyException() : base ("Invalid reply") {}
    }
}