[![Build Status](https://dev.azure.com/dlar/RabbitMQ.TraceableMessaging/_apis/build/status/dmlarionov.RabbitMQ.TraceableMessaging?branchName=master)](https://dev.azure.com/dlar/RabbitMQ.TraceableMessaging/_build/latest?definitionId=5&branchName=master)

# RabbitMQ.TraceableMessaging
## Overview

The repository contains .NET libraries for RPC over RabbitMQ with the following features:

1. Distributed traceability.
2. Bearer token authorization.

How can you benefit from this learn from [example project](https://github.com/dmlarionov/RabbitMQ.TraceableMessaging-example1).

## Projects

Repository contains:

1. `RabbitMQ.TraceableMessaging` - core library.
2. `RabbitMQ.TraceableMessaging.ApplicationInsights` - implementation for Application Insights.
3. `RabbitMQ.TraceableMessaging.Json` - JSON serialization.
4. `RabbitMQ.TraceableMessaging.Jwt` - JWT support.
5. `RabbitMQ.TraceableMessaging.YamlDotNet` - YAML serialization.

## How to use

In your service and client projects add references to:

- `RabbitMQ.TraceableMessaging` package.
- Serialization package (`RabbitMQ.TraceableMessaging.Json` or `RabbitMQ.TraceableMessaging.YamlDotNet`).
- `RabbitMQ.TraceableMessaging.ApplicationInsights` or your own implementation.
- `RabbitMQ.TraceableMessaging.Jwt` or your own implementation.

Create request and response types in library project then reference to it from both service and clients. Reply types have to inherit from `RabbitMQ.TraceableMessaging.Models.Reply`.

Simple service class built from scratch may look like:

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

Simple client may be similar to:

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

Request from a client to a service can be make this way:

```csharp
var request = new RequestA();
var response = RpcClient.GetReply<ResponseA>(request: request);
```

## Exceptions can be thrown

Exceptions defined in namespace `RabbitMQ.TraceableMessaging.Exceptions` can be thrown:

- `ForbiddenException` - The server understood the request but refuses to authorize it. Equivalent of HTTP 403.
- `UnauthorizedException` - The request lacks authentication credentials. Like HTTP 401. Should lead to authorization round trip.
- `RequestFailureException` - The server cannot process due to something. Like HTTP 500.
- `InvalidReplyException` - The client can't read the response.

Following exceptions from other namespaces can be thrown: 

`System.TimeoutException` - reply didn't arrive in time.

### Example

Usage patterns you may get from [example project](https://github.com/dmlarionov/RabbitMQ.TraceableMessaging-example1).
