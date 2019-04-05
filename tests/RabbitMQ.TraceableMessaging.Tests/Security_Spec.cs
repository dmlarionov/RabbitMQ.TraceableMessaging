using Newtonsoft.Json;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.Tests.Fixtures;
using RabbitMQ.TraceableMessaging.Tests.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RabbitMQ.TraceableMessaging.Tests
{
    public class Security_Spec : IClassFixture<SecurityFixture<JsonFormatOptions>>
    {
        SecurityFixture<JsonFormatOptions> Fixture;
        string UserId = Guid.NewGuid().ToString();

        public Security_Spec(SecurityFixture<JsonFormatOptions> fixture)
        {
            Fixture = fixture;
        }

        [Fact]
        public async Task UnauthorizedNoToken()
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                Fixture.Client.GetReplyAsync<Pong1>(new Ping1()));
        }

        [Fact]
        public async Task UnauthorizedInvalidToken()
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                Fixture.Client.GetReplyAsync<Pong1>(new Ping1(),
                    accessToken: JsonConvert.SerializeObject(
                        new Token
                        {
                            UserIdentity = UserId,
                            ValidationBehaviour = TokenValidationBehaviour.Unauthorized,
                            Scopes = new string[] { "Ping1" }
                        })));
        }

        [Fact]
        public async Task ForbiddenInvalidToken()
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Fixture.Client.GetReplyAsync<Pong1>(new Ping1(), 
                    accessToken: JsonConvert.SerializeObject(
                        new Token {
                            UserIdentity = UserId,
                            ValidationBehaviour = TokenValidationBehaviour.Forbidden,
                            Scopes = new string[] { "Ping1" }
                        })));
        }

        [Fact]
        public async Task ForbiddenNoScope()
        {
            // We don't pass "Ping1" due to unexistence of it in the scopes
            // (checked by TestSecurityOptions.Authorize delegate)
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Fixture.Client.GetReplyAsync<Pong1>(new Ping1(),
                    accessToken: JsonConvert.SerializeObject(
                        new Token
                        {
                            UserIdentity = UserId,
                            ValidationBehaviour = TokenValidationBehaviour.Pass,
                            Scopes = new string[] { "Ping2" }
                        })));
        }

        [Fact]
        public async Task PassedCheck()
        {
            await Fixture.Client.GetReplyAsync<Pong1>(new Ping1(),
                    accessToken: JsonConvert.SerializeObject(
                        new Token
                        {
                            UserIdentity = UserId,
                            ValidationBehaviour = TokenValidationBehaviour.Pass,
                            Scopes = new string[] { "Ping1" }
                        }));
        }

        [Fact]
        public async Task SkippedCheckForRequestType()
        {
            // We pass "Ping2" despite unexistence of it in the scopes
            // due to TestSecurityOptions.SkipForRequestTypes exclusion
            await Fixture.Client.GetReplyAsync<Pong2>(new Ping2(),
                    accessToken: JsonConvert.SerializeObject(
                        new Token
                        {
                            UserIdentity = UserId,
                            ValidationBehaviour = TokenValidationBehaviour.Pass,
                            Scopes = new string[] { "Ping1" }
                        }));
        }
    }
}
