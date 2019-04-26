[![Build Status](https://dev.azure.com/dlar/RabbitMQ.TraceableMessaging/_apis/build/status/dmlarionov.RabbitMQ.TraceableMessaging?branchName=master)](https://dev.azure.com/dlar/RabbitMQ.TraceableMessaging/_build/latest?definitionId=5&branchName=master)

# RabbitMQ.TraceableMessaging
## Overview

The repository contains .NET libraries for RPC over RabbitMQ with the following features:

1. Distributed traceability.
2. Bearer token authorization.

## Projects

Repository contains:

1. `RabbitMQ.TraceableMessaging` - core library.
2. `RabbitMQ.TraceableMessaging.ApplicationInsights` - implementation of distributed tracing for Application Insights.
3. `RabbitMQ.TraceableMessaging.Json` - JSON serialization.
4. `RabbitMQ.TraceableMessaging.Jwt` - JWT support.
5. `RabbitMQ.TraceableMessaging.YamlDotNet` - YAML serialization.

## How to use

In your service and client projects add references to:

- `RabbitMQ.TraceableMessaging` package.
- One of serialization packages (otherwise you may use `RabbitMQ.TraceableMessaging.Options.FormatOptions` class with your delegates and `ContentType` value assigned).
- `RabbitMQ.TraceableMessaging.ApplicationInsights` or your own implementation for any other distributed tracing system.
- `RabbitMQ.TraceableMessaging.Jwt` or your own implementation for any other token type.

Create request and response types. Do it in library project to include it to your service and to its clients. Reply types have to inherit from `RabbitMQ.TraceableMessaging.Models.Reply`.

Simplest service looks like:

```csharp
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.Jwt.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights;
...

public sealed class Service : IDisposable
{
	protected IModel Channel { get; set; }
	protected RpcServer<JwtSecurityContext> RpcServer { get; set; }
	protected TelemetryClient TelemetryClient { get; set; }
	
	public Service(IConnection conn, TelemetryClient telemetryClient)
	{
		// keep telemetry client reference to use for exception tracking
		TelemetryClient = telemetryClient ?? new TelemetryClient(TelemetryConfiguration.Active);
		
		// create channel over RabbitMQ connection
		Channel = conn.CreateModel();
		
		// declare request queue
		Channel.QueueDeclare("service_queue_name");
		
		// configure consume options
		var consumeOptions = new ConsumeOptions();
		consumeOptions.Queue = "service_queue_name";
		
		// create RPC server instance
		RpcServer = new RpcServer<JwtSecurityContext>(
			Channel,
			consumeOptions, 
			new JsonFormatOptions(),
			null,	// null - skip token validation and authorization
			TelemetryClient);
		
		// subscribe to events
		RpcServer.Received += OnReceive;
	}
	
	void OnReceive(object sender, RequestEventArgs<TelemetryContext, JwtSecurityContext> ea)
	{
		try
		{
			switch (ea.RequestType)
			{
				// Request A
				case nameof(RequestA):
				...	// create reply for request A
				Server.Reply(ea.CorrelationId, reply);
				break;
				
				// Request B
				case nameof(RequestB):
				...	// create reply for request B
				Server.Reply(ea.CorrelationId, reply);
				break;
				
				// any other case explores in the end
				default:
				throw new Exception("Unsupported request type!");
			}
		}
		catch(Exception e)
		{
			// track exception on server side to telemetry
			TelemetryClient.TrackException(e);
			
			// reply with failure
			RpcServer.Reply(ea.CorrelationId, new Reply { Status = ReplyStatus.Fail });
		}
	}

	public void Dispose()
	{
		RpcServer.Dispose();
		Channel.Dispose();
	}
}
```

Simplest client looks like:

```csharp
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.Jwt.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights;
...

public sealed class Client : IDisposable
{
	protected IModel Channel { get; set; }
	protected RpcClient RpcClient { get; set; }
	protected TelemetryClient TelemetryClient { get; set; }
	
	public Client(IConnection conn, TelemetryClient telemetryClient)
	{
		// keep telemetry client reference to use for exception tracking
		TelemetryClient = telemetryClient ?? new TelemetryClient(TelemetryConfiguration.Active);
		
		// create channel over RabbitMQ connection
		Channel = conn.CreateModel();
		
		// declare response queue
		responseQueue = "reply-to-" + $"{Guid.NewGuid().ToString()}";
		Channel.QueueDeclare(
			queue: responseQueue,
			durable: false,
			exclusive: true,
			autoDelete: true);
		
		// configure publish options
		var publishOptions = new PublishOptions();
		publishOptions.RoutingKey = "service_queue_name";
		
		// configure consume options
		var consumeOptions = new ConsumeOptions();
		consumeOptions.AutoAck = true;
		consumeOptions.Queue = responseQueue;
		
		// create RPC client instance
		RpcClient = new RpcClient(
			Channel,
			publishOptions,
			consumeOptions, 
			new JsonFormatOptions(),
			TelemetryClient);
		}
	}
	
	public void Dispose()
	{
		Channel.Dispose();
	}
}
```

Simplest request to service on client side looks like:

```csharp
var request = new RequestA();
var response = RpcClient.GetReply<ResponseA>(request: request);
```

## Exceptions

Following exceptions defined in `RabbitMQ.TraceableMessaging.Exceptions` can be thrown:

- `ForbiddenException` - The server understood the request but refuses to authorize it. Like HTTP 403.
- `UnauthorizedException` - The request has not been applied because it lacks valid authentication credentials for the target resource. Like HTTP 401. Should lead to authorization round trip on client side.
- `RequestFailureException` - The server cannot or will not process the request due to something that is perceived to be a client error. Like HTTP 400.
- `InvalidReplyException` - Library at client side can't read response.

`System.TimeoutException` can be thrown, which means than reply didn't arrive after a time specified in request or set as default timeout through `RpcClient` properties (1 minute by default).

`ForbiddenException`  and `UnauthorizedException`  can be thrown only if `SecurityOptions` are passed to constructor of `RpcServer<..>`.

## Authorization

There are two classes related to security you have to understand.

*Security context* - type parameter of  `RpcServer<..>`. Security context is introduced to:

- Keep security info required for request authorization, at least validated token and principal.
- Keep some additional information to use in telemetry related to security. Token issuer for instance.

*Security options* - object passed to `RpcServer<..>` constructor. It configures behavior:

- Token validation and creation of security context through `CreateSecurityContext` delegate.
- Authorization through `Authorize` delegate.
- Skipping for any security for certain requests through `SkipForRequestTypes` collection.

`RabbitMQ.TraceableMessaging.Jwt` contains implementation for both context and options based on JWT, but you have to configure `Authorize` by implementing your own rules of authorization.

### Example

Imagine you have mapping between request types and access token scopes in dictionary:

```csharp
private IDictionary<string, string> Map { get; set; }
```

You want to authorize requests by checking for mapped scope:

```csharp
public AuthzResult AuthorizeFunc(string requestType, JwtSecurityContext context)
{
	string checkScope;
	if (!Map.TryGetValue(requestType, out checkScope))
		return new AuthzResult(false, $"No scope defined for {requestType}");
	
	if (context.Principal.Claims.Where(c => c.Type == "scope" && c.Value == checkScope).Any())
		return new AuthzResult(true);	// permitted
	else
		return new AuthzResult(false, $"Scope '{checkScope}' is required for request of type '{requestType}' to the service");	// forbidden
}
```

There can be any logic, but simple mapping is the most relevant example for majority of scenarios.

You have to create `TokenValidationParameters` to validate token itself ('is it correctly issued?'):

```csharp
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
...

var p = new TokenValidationParameters 
{
	ValidAudience = "service_name",
	ValidIssuer = "authority_url"
};

await setKeysAsync("authority_url", keys => p.IssuerSigningKeys = keys);

...
/// <summary>
/// Set issuer signing keys by looking to Open Id Connect metadata
/// </summary>
private static async Task setKeysAsync(string AuthorityUrl, Action<ICollection<SecurityKey>> setKeysFunc)
{
	string OpenIdConnectUrl = $"{AuthorityUrl}/.well-known/openid-configuration";
	try
	{
		IConfigurationManager<OpenIdConnectConfiguration> configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(OpenIdConnectUrl, new OpenIdConnectConfigurationRetriever());
		OpenIdConnectConfiguration openIdConfig = await configurationManager.GetConfigurationAsync(CancellationToken.None);
		setKeysFunc(openIdConfig.SigningKeys);
	}
	catch(Exception e)
	{
		throw new Exception($"Can't reach {OpenIdConnectUrl}", e);
	}
}
```

Create your `JwtSecurityOptions` and instantiate  `RpcServer<JwtSecurityContext>` based on it:

```csharp
var options = new JwtSecurityOptions(p)	// p is TokenValidationParameters
{
	Authorize = AuthorizeFunc	// AuthorizeFunc based on mapping is declared above
};

// create RPC server instance
RpcServer = new RpcServer<JwtSecurityContext>(
	Channel,
	consumeOptions, 
	new JsonFormatOptions(),
	JwtSecurityOptions,	// validate token and authorize
	TelemetryClient);
```

Done! You have protected your API over RabbitMQ.

## Usage without authorization

If you don't need authorization at all then create your own stub implementation of `RabbitMQ.TraceableMessaging.Models.SecurityContext` or just use `RabbitMQ.TraceableMessaging.Jwt` as type parameter without passing any `SecurityOptions` to `RpcServer<..>` constructor.

If you have already implemented RPC without authorization (as shown above), but you are going to extend your service with security while keeping backward compatibility, then use `SecurityOptions.SkipForRequestTypes` collection for request types that have to stay unprotected.

If you need a service that serves a public and protected request types then you can put your public request types into `SecurityOptions.SkipForRequestTypes`.
