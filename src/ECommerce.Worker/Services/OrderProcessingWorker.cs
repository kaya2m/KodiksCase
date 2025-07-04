using ECommerce.Application.Services;
using ECommerce.Application.Services.Interface;
using ECommerce.Core.Events;
using ECommerce.Shared.Constants;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace ECommerce.Worker.Services;

public class OrderProcessingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderProcessingWorker> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public OrderProcessingWorker(
        IServiceProvider serviceProvider,
        ILogger<OrderProcessingWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: QueueNames.ORDER_PLACED,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Processing Worker started");

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                _logger.LogInformation("Received order processing message: {Message}", message);

                var orderEvent = JsonSerializer.Deserialize<OrderPlacedEvent>(message);
                if (orderEvent != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var orderProcessingService = scope.ServiceProvider.GetRequiredService<IOrderProcessingService>();

                    await orderProcessingService.ProcessOrderAsync(orderEvent);

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    _logger.LogInformation("Order {OrderId} processed successfully", orderEvent.OrderId);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize order event: {Message}", message);
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order message: {Message}", message);

                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: QueueNames.ORDER_PLACED,
            autoAck: false,
            consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}