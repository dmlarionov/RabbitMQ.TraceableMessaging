using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.TraceableMessaging.Tests.Models
{
    enum TokenValidationBehaviour
    {
        Pass,
        Unauthorized,
        Forbidden
    }
}
