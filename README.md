# RabbitMQ.TraceableMessaging
.NET library supporting implementation of RPC pattern over RabbitMQ with features:
1. Support for distributed traceability (Application Insights, OpenTracing etc.).
2. Optional bearer token and authorization. Made to support development of secure RabbitMQ based microservices.

Repository contains:
1. `RabbitMQ.TraceableMessaging` - core library with serialization code and security context abstracted away.
2. `RabbitMQ.TraceableMessaging.ApplicationInsights` - complete implementation for Application Insights and JWT.
3. `RabbitMQ.TraceableMessaging.Json` - JSON serialization.
4. `RabbitMQ.TraceableMessaging.Jwt` - JWT support.
5. `RabbitMQ.TraceableMessaging.YamlDotNet` - YAML serialization.

Core library on client side cares about setting headers, sending message, awaiting response and throwing exceptions:

- `ForbiddenException` in case of failing authorization on server (if use token). Shouldn't lead to round trips on client side, just no access.
- `UnauthorizedException` if server can't read token (if use it) or see it's expired. Should lead to authorization round trip on client side.
- `RequestFailureException` means server had some error during execution.
- `InvalidReplyException` means core library at client side can't read response.
- `TimeoutException` if message didn't arrive after specified time.

... 

Serialization (JSON and YAML) support libraries implement methods to serialize and deseriaze using Newtonsoft.Json and YamlDotNet respectively.