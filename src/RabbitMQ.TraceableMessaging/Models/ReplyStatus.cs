namespace RabbitMQ.TraceableMessaging.Models
{
    /// <summary>
    /// Status of reply
    /// </summary>
    public enum ReplyStatus
    {
        /// <summary>
        /// Request is processed successfully
        /// </summary>
        Success = 0,

        /// <summary>
        /// Error in processing of request
        /// </summary>
        Fail = -1,

        /// <summary>
        /// No valid access token (initialize authorization round-trip on client side)
        /// </summary>
        Unauthorized = -2,

        /// <summary>
        /// Access forbidden (show forbidden screen on client side)
        /// </summary>
        Forbidden = -3
    }    
}