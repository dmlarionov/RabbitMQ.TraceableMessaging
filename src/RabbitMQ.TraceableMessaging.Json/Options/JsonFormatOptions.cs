using System;
using System.IO;
using System.Text;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Exceptions;
using Newtonsoft.Json;

namespace RabbitMQ.TraceableMessaging.Json.Options
{
    public class JsonFormatOptions : RabbitMQ.TraceableMessaging.Options.FormatOptions
    {
        public JsonFormatOptions()
        {
            ContentType = "application/json";

            CreateBytesFromObject = (@object) =>
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@object));

            CreateObjectFromBytes = (body, type) =>
                JsonConvert.DeserializeObject(Encoding.UTF8.GetString(body), type);
        }

        public JsonFormatOptions(JsonSerializerSettings settings)
        {
            ContentType = "application/json";

            CreateBytesFromObject = (@object) =>
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@object, settings));

            CreateObjectFromBytes = (body, type) =>
                JsonConvert.DeserializeObject(Encoding.UTF8.GetString(body), type);
        }
    }
}