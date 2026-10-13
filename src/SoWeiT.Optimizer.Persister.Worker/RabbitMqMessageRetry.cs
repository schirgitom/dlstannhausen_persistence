using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SoWeiT.Optimizer.Persister.Worker;

internal static class RabbitMqMessageRetry
{
    public static bool Retry(IModel channel, BasicDeliverEventArgs args, string queueName, string headerName, int maxRetryCount)
    {
        var currentRetryCount = GetRetryCount(args.BasicProperties, headerName);
        if (currentRetryCount >= maxRetryCount)
        {
            channel.BasicNack(deliveryTag: args.DeliveryTag, multiple: false, requeue: false);
            return false;
        }

        var retryProperties = channel.CreateBasicProperties();
        retryProperties.Persistent = args.BasicProperties?.Persistent ?? true;
        retryProperties.ContentType = args.BasicProperties?.ContentType;
        retryProperties.ContentEncoding = args.BasicProperties?.ContentEncoding;
        retryProperties.CorrelationId = args.BasicProperties?.CorrelationId;
        retryProperties.MessageId = args.BasicProperties?.MessageId;
        retryProperties.Type = args.BasicProperties?.Type;
        retryProperties.Timestamp = args.BasicProperties?.Timestamp ?? default;
        retryProperties.Headers = new Dictionary<string, object>(args.BasicProperties?.Headers ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
        retryProperties.Headers[headerName] = currentRetryCount + 1;

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: retryProperties,
            body: args.Body);

        channel.BasicAck(deliveryTag: args.DeliveryTag, multiple: false);
        return true;
    }

    private static int GetRetryCount(IBasicProperties? properties, string headerName)
    {
        if (properties?.Headers is null || !properties.Headers.TryGetValue(headerName, out var value))
        {
            return 0;
        }

        return value switch
        {
            byte b => b,
            sbyte sb => sb,
            short s => s,
            ushort us => us,
            int i => i,
            uint ui => (int)ui,
            long l => (int)l,
            ulong ul => (int)ul,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            _ => 0
        };
    }
}
