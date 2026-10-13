using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using SoWeiT.Optimizer.Messaging.RabbitMq;
using SoWeiT.Optimizer.Persistence.History.Persistence;

namespace SoWeiT.Optimizer.Persister.Worker;

public sealed class RabbitMqFieldDataConsumer : BackgroundService
{
    private const string RetryHeaderName = "x-fielddata-retry-count";
    private readonly ILogger<RabbitMqFieldDataConsumer> _logger;
    private readonly IOptimizerFieldDataStore _store;
    private readonly RabbitMqHistoryOptions _options;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqFieldDataConsumer(
        IConfiguration configuration,
        IOptimizerFieldDataStore store,
        ILogger<RabbitMqFieldDataConsumer> logger)
    {
        _store = store;
        _logger = logger;
        _options = configuration.GetSection("RabbitMq").Get<RabbitMqHistoryOptions>() ?? new RabbitMqHistoryOptions();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueName = _options.FieldDataQueueName;
        _logger.LogInformation(
            "Connecting field data consumer: Host={Host}, Port={Port}, VirtualHost={VirtualHost}, Queue={QueueName}",
            _options.HostName, _options.Port, _options.VirtualHost, queueName);
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        try
        {
            _connection = factory.CreateConnection("optimizer-persister-fielddata-consumer");
            _logger.LogInformation("Field data RabbitMQ connection established; declaring queue {QueueName}", queueName);
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _logger.LogInformation("Field data queue {QueueName} declared; registering consumer", queueName);
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (_, args) => HandleMessage(args);
            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }
        catch (BrokerUnreachableException ex)
        {
            _logger.LogCritical(ex, "Field data consumer cannot connect to RabbitMQ at {Host}:{Port}, virtual host {VirtualHost}",
                _options.HostName, _options.Port, _options.VirtualHost);
            throw;
        }
        catch (OperationInterruptedException ex)
        {
            _logger.LogCritical(ex, "Field data consumer cannot declare or consume queue {QueueName} in virtual host {VirtualHost}",
                queueName, _options.VirtualHost);
            throw;
        }
        _logger.LogInformation("Field data consumer connected to RabbitMQ queue {QueueName}", queueName);

        stoppingToken.Register(() =>
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while closing field data RabbitMQ connection");
            }
        });

        return Task.CompletedTask;
    }

    private void HandleMessage(BasicDeliverEventArgs args)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException("Field data RabbitMQ channel is not available.");
        }

        try
        {
            _logger.LogDebug("Processing field data delivery {DeliveryTag}", args.DeliveryTag);
            var message = JsonSerializer.Deserialize<OptimizerFieldData>(args.Body.Span, _serializerOptions)
                ?? throw new InvalidOperationException("Could not deserialize field data message.");
            _store.Append(message);
            _channel.BasicAck(deliveryTag: args.DeliveryTag, multiple: false);
            _logger.LogDebug(
                "Field data delivery {DeliveryTag} persisted and acknowledged: Type={Type}, Station={Station}, Variable={Variable}",
                args.DeliveryTag, message.Type, message.Station, message.Variable);
        }
        catch (Exception ex)
        {
            if (RabbitMqMessageRetry.Retry(_channel, args, _options.FieldDataQueueName, RetryHeaderName, _options.MaxRetryCount))
            {
                _logger.LogError(
                    ex, "Field data delivery {DeliveryTag} failed on queue {QueueName}; retry scheduled (max {MaxRetryCount})",
                    args.DeliveryTag, _options.FieldDataQueueName, _options.MaxRetryCount);
                return;
            }

            _logger.LogError(
                ex, "Field data delivery {DeliveryTag} failed on queue {QueueName}; max retries reached ({MaxRetryCount}), message rejected",
                args.DeliveryTag, _options.FieldDataQueueName, _options.MaxRetryCount);
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
