[![Build Status](https://dev.azure.com/dlar/RabbitMQ.TraceableMessaging/_apis/build/status/dmlarionov.RabbitMQ.TraceableMessaging?branchName=master)](https://dev.azure.com/dlar/RabbitMQ.TraceableMessaging/_build/latest?definitionId=5&branchName=master)

# RabbitMQ.TraceableMessaging
## Overview

The repository contains .NET libraries for RPC over RabbitMQ with the following features:

1. Distributed traceability.
2. Bearer token authorization.

Learn how you can benefit from this at [example project](https://github.com/dmlarionov/RabbitMQ.TraceableMessaging-example1).

## Projects

The repository contains:

1. `RabbitMQ.TraceableMessaging` - core library.
2. `RabbitMQ.TraceableMessaging.ApplicationInsights` - implementation for Application Insights.
3. `RabbitMQ.TraceableMessaging.Json` - JSON serialization.
4. `RabbitMQ.TraceableMessaging.Jwt` - JWT support.
5. `RabbitMQ.TraceableMessaging.YamlDotNet` - YAML serialization.

## How to use

In your service and client projects add references to:

- `RabbitMQ.TraceableMessaging` package.
- `RabbitMQ.TraceableMessaging.Json` or `RabbitMQ.TraceableMessaging.YamlDotNet`.
- `RabbitMQ.TraceableMessaging.ApplicationInsights` or your own implementation.
- `RabbitMQ.TraceableMessaging.Jwt` or your own implementation.

Create request and response types in some library project then reference to it from both service and clients. Reply types have to inherit from `RabbitMQ.TraceableMessaging.Models.Reply` class.

Your simplest (without authorization) service class may look like:

```csharp
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.Jwt.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights;
...

public sealed class MyService
{
	private RpcServer<JwtSecurityContext> RpcServer { get; set; }
	
	public MyService(IConnection conn)
	{
		// create channel over RabbitMQ connection
		var channel = conn.CreateModel();
		
		// declare request queue (from clients to service)
		channel.QueueDeclare("service_queue_name");
		
		// create RPC server instance
		RpcServer = new RpcServer<JwtSecurityContext>(
			channel,
			new ConsumeOptions(queue), 
			new JsonFormatOptions());
		
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
					...	// do job for request A
					Server.Reply(ea.CorrelationId, reply);
					break;
				
				// Request B
				case nameof(RequestB):
					...	// do job for request B
					Server.Reply(ea.CorrelationId, reply);
					break;
				
				// Other request type
				default:
					throw new NotImplementedException($"{ea.RequestType} is not implemented");
			}
		}
		catch(Exception e)
		{
			// track exception and reply with failure
			RpcServer.Fail(ea, ex);
		}
	}
}
```

In real application you probably wish to add authorization and make service class a hosted service (`IHostedService`). See [example project](https://github.com/dmlarionov/RabbitMQ.TraceableMessaging-example1) to learn how to do it.

Your simplest client class can be similar to:

```csharp
using Microsoft.ApplicationInsights.Extensibility;
using RabbitMQ.Client;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Json.Options;
using RabbitMQ.TraceableMessaging.Jwt.Options;
using RabbitMQ.TraceableMessaging.ApplicationInsights;
...

public sealed class MyClient
{
	protected RpcClient RpcClient { get; set; }
	
	public MyClient(IConnection conn)
	{
		// create channel over RabbitMQ connection
		var channel = conn.CreateModel();
		
		// declare response queue (from service to client)
		responseQueue = $"reply-to-{Guid.NewGuid().ToString()}";
		channel.QueueDeclare(
			queue: responseQueue,
			durable: false,
			exclusive: true,
			autoDelete: true);
		
		// create RPC client instance
		RpcClient = new RpcClient(
			channel,
			new PublishOptions("service_queue_name"),
			new ConsumeOptions(responseQueue), 
			new JsonFormatOptions());
		}
	}
	
	... // some application methods
}
```

Requests in the client class application methods can be made this way:

```csharp
var request = new RequestA();
var response = RpcClient.GetReply<ResponseA>(request: request);
```

For authorized request pass access token as an argument to `RpcClient.GetReply<ResponseA>(request: request, accessToken: token)`.

## Exceptions can be thrown

Exceptions defined in namespace `RabbitMQ.TraceableMessaging.Exceptions` can be thrown:

- `ForbiddenException` - The server understood the request but refuses to authorize it. Equivalent of HTTP 403.
- `UnauthorizedException` - Lacks of valid authorization token. Like HTTP 401. Should lead to authorization round trip.
- `RequestFailureException` - The server cannot process due to something. Like HTTP 500.
- `InvalidReplyException` - The client can't read the response.

Following exceptions from other namespaces can be thrown by the client: 

`System.TimeoutException` - reply didn't arrive in time.

### Example

You may get more usage patterns from [example project](https://github.com/dmlarionov/RabbitMQ.TraceableMessaging-example1).
