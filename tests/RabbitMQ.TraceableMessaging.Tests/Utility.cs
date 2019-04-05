using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests
{
    static class Utility
    {
        public static ConnectionFactory GetConnectionFactory() =>
            new ConnectionFactory()
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                VirtualHost = "/"
            };


        public static IConnection CreateConnection() =>
            GetConnectionFactory().CreateConnection();
    }
}
