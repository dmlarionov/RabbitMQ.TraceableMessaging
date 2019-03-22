using System.Security.Claims;

namespace RabbitMQ.TraceableMessaging.Models
{
    /// <summary>
    /// Authorization Result
    /// </summary>
    public class AuthzResult
    {
        /// <summary>
        /// Is user / client authorized to access service?
        /// </summary>
        public bool IsAuthorized { get; set; }

        /// <summary>
        /// Authorization error (if any)
        /// </summary>
        public string Error { get; set; }

        public AuthzResult(bool isAuthorized, string error = null)
        {
            IsAuthorized = isAuthorized;
            Error = error;
        }
    }
}