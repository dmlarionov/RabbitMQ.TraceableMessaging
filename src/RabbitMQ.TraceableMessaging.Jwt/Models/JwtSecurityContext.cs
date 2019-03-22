using System.IdentityModel.Tokens.Jwt;
using RabbitMQ.TraceableMessaging.Models;

namespace RabbitMQ.TraceableMessaging.Jwt.Models
{
    public class JwtSecurityContext : SecurityContext
    {
        public JwtSecurityToken ValidatedAccessToken { get; set; }
    }
}