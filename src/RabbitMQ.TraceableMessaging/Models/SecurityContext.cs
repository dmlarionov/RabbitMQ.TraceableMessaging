using System.Security.Claims;

namespace RabbitMQ.TraceableMessaging.Models
{
    public class SecurityContext
    {
        public ClaimsPrincipal Principal { get; set; }
    }
}