using System;
using System.IO;
using System.Text;
using RabbitMQ.TraceableMessaging.Options;
using RabbitMQ.TraceableMessaging.Exceptions;
using YamlDotNet.Serialization;

namespace RabbitMQ.TraceableMessaging.YamlDotNet.Options
{
    public class YamlFormatOptions : RabbitMQ.TraceableMessaging.Options.FormatOptions
    {
        public YamlFormatOptions()
        {
            ContentType = "application/x-yaml";

            CreateBytesFromObject = (@object) =>
            {
                StringWriter requestWriter = new StringWriter();
                (new Serializer()).Serialize(requestWriter, @object);
                return Encoding.UTF8.GetBytes(requestWriter.ToString());
            };

            CreateObjectFromBytes = (body, type) =>
            {
                return (new Deserializer()).Deserialize(
                    new StringReader(Encoding.UTF8.GetString(body)), type);
            };
        }
    }
}