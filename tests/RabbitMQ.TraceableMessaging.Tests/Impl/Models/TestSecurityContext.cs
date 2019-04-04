using RabbitMQ.TraceableMessaging.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests.Impl.Models
{
    public class TestSecurityContext : SecurityContext
    {
        public string UserIdentity { get; set; }
        public string[] Scopes { get; set; }
    }
}
