using RabbitMQ.TraceableMessaging.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Impl.Models
{
    public class TestSecurityContext : SecurityContext
    {
        public string[] Scopes { get; set; }
    }
}
