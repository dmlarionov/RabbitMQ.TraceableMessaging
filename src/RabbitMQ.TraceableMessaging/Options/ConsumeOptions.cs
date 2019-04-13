namespace RabbitMQ.TraceableMessaging.Options
{
    public class ConsumeOptions
    {
        public string Queue { get; set; }
        public bool AutoAck { get; set; } = false;
        public ConsumeOptions() { }
        public ConsumeOptions(string queue) => Queue = queue;
    }
}