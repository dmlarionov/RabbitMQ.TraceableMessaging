using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.TraceableMessaging.Exceptions;
using RabbitMQ.TraceableMessaging.Jwt.Models;

namespace RabbitMQ.TraceableMessaging.Jwt.Options
{
    /// <summary>
    /// Settings and delegates for security implementation
    /// </summary>
    public class JwtSecurityOptions : RabbitMQ.TraceableMessaging.Options.SecurityOptions<JwtSecurityContext>
    {
        public JwtSecurityOptions(TokenValidationParameters tokenValidationParameters)
        {
            CreateSecurityContext = (accessTokenEncoded) => 
            {
            
                // create new empty security context
                var context = new JwtSecurityContext();

                // validate token and create principal
                try
                {
                    SecurityToken token;
                    context.Principal = new JwtSecurityTokenHandler()
                        .ValidateToken(accessTokenEncoded,
                            tokenValidationParameters,
                            out token);
                    context.ValidatedAccessToken = (JwtSecurityToken)token;
                    context.AccessTokenEncoded = accessTokenEncoded;
                }
                catch(ArgumentException argex)
                {
                    // the token was not well-formed or was invalid for some other reason.
                    throw new UnauthorizedException($"Token was invalid: {argex.Message}");
                }
                catch(SecurityTokenInvalidAudienceException stiae)
                {
                    throw new ForbiddenException($"Invalid token audience: {stiae.InvalidAudience}");
                }
                catch(SecurityTokenInvalidIssuerException stiie)
                {
                    throw new ForbiddenException($"Invalid token issuer: {stiie.InvalidIssuer}");
                }
                catch(SecurityTokenInvalidLifetimeException stile)
                {
                    throw new ForbiddenException($"Token expired: {stile.Expires}");
                }
                catch(SecurityTokenInvalidSignatureException stiss)
                {
                    throw new ForbiddenException($"Token signature exception: {stiss.Message}");
                }
                catch(SecurityTokenInvalidSigningKeyException stiske)
                {
                    throw new ForbiddenException($"Invalid token signing key exception: {stiske.Message}");
                }
                catch(SecurityTokenValidationException stvex)
                {
                    // the token failed validation!
                    throw new ForbiddenException($"Token failed validation: {stvex.Message}");
                }

                return context;
            };
        }
    }
}