namespace RabbitMQ.TraceableMessaging.Models
{
    /// <summary>
    /// Base class for reply
    /// </summary>
    public class Reply
    {
        /// <summary>
        /// Response status
        /// </summary>
        public ReplyStatus Status { get; set; } = ReplyStatus.Success;

        /// <summary>
        /// Error message (in case of failure)
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}