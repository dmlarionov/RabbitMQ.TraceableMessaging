using RabbitMQ.TraceableMessaging.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests.Models
{
    class Pong2 : Reply
    {
        public string Payload { get; set; }

        public Pong2() { }

        public Pong2(string payload) => Payload = payload;
    }
}
