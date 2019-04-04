using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests.Models
{
    class Token
    {
        public string UserIdentity { get; set; }
        public TokenValidationBehaviour ValidationBehaviour { get; set; }
        public string[] Scopes { get; set; }
    }
}
