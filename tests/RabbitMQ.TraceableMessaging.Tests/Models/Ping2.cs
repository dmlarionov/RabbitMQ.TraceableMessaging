using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests.Models
{
    class Ping2
    {
        public string Payload { get; set; }

        public Ping2() { }

        public Ping2(string payload) => Payload = payload;
    }
}
