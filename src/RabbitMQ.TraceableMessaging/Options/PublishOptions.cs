namespace RabbitMQ.TraceableMessaging.Options
{
    public class PublishOptions
    {
        public string Exchange { get; set; } = "";
        public string RoutingKey { get; set; }
    }
}