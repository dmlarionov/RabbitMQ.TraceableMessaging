using System;
using RabbitMQ.TraceableMessaging.Options;

namespace RabbitMQ.TraceableMessaging.EventArgs
{
    public class RequestEventArgsBase : System.EventArgs
    {
        /// <summary>
        /// Request Type
        /// </summary>
        public string RequestType { get; private set; }

        /// <summary>
        /// Request Body
        /// </summary>
        public byte[] Body { get; private set; }

        /// <summary>
        /// Get Request Object
        /// </summary>
        public TRequest GetRequest<TRequest>()
        {
            return (TRequest)(_formatOptions.CreateObjectFromBytes(Body, typeof(TRequest)));
        }

        /// <summary>
        /// Options to deserialize request
        /// </summary>
        private FormatOptions _formatOptions;

        public RequestEventArgsBase(
            string requestType,
            byte[] body,
            FormatOptions formatOptions)
        {
            if (requestType == null)
                throw new ArgumentNullException(nameof(requestType));
            else
                RequestType = requestType;
            
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            else
                Body = body;

            if (formatOptions == null)
                throw new ArgumentNullException(nameof(formatOptions));
            else
                _formatOptions = formatOptions;
        }
    }
}