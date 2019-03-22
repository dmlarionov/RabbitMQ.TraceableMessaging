using System;

namespace RabbitMQ.TraceableMessaging.Options
{
    /// <summary>
    /// Settings and delegates for serialization and deserialization
    /// </summary>
    public class FormatOptions
    {
        /// <summary>
        /// Content Type name RabbitMQ header
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Serialization delegate
        /// </summary>
        public Func<object, byte[]> CreateBytesFromObject { get; set; }

        /// <summary>
        /// Deserialization deletage
        /// </summary>
        public Func<byte[], Type, object> CreateObjectFromBytes { get; set; }
    }
}