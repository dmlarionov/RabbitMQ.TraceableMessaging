using System.Security.Claims;

namespace RabbitMQ.TraceableMessaging.Models
{
    public class SecurityContext
    {
        public ClaimsPrincipal Principal { get; set; }
        public virtual string AccessTokenEncoded { get; set; }
        public virtual string AccessTokenIssuer { get; set; }
    }
}