using Newtonsoft.Json;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Models;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Impl.Models;
using RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace RabbitMQ.TraceableMessaging.ApplicationInsights.Tests.Impl.Options
{
    public class TestSecurityOptions : SecurityOptions<TestSecurityContext>
    {
        public TestSecurityOptions()
        {
            CreateSecurityContext = (accessTokenEncoded) =>
            {
                var token = JsonConvert.DeserializeObject<Token>(accessTokenEncoded);
                return new TestSecurityContext
                {
                    Principal = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            token.Scopes.Select(s => new Claim("scope", s))
                                .Append(new Claim("sub", token.UserIdentity)),
                            "test", // authentication type
                            "sub",  // name claim type (we put it there)
                            null)), // role type (we don't have it)
                    Scopes = token.Scopes
                };
            };

            Authorize = (string requestType, TestSecurityContext context) =>
            {
                if (context.Scopes.Contains(requestType))
                    return new AuthzResult(true, null);

                else
                    return new AuthzResult(false, "Request type forbidden!");
            };
        }
    }
}
