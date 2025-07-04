using ECommerce.Core.Interfaces.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

namespace ECommerce.Infrastructure.Services
{
    public class RabbitMQPublisher : IMessagePublisher, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQPublisher> _logger;
        private readonly object _lockObject = new object();
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed = false;

        public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _semaphore = new SemaphoreSlim(1, 1);

            try
            {
                var factory = CreateConnectionFactory(configuration);
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.ConfirmSelect();

                _logger.LogInformation("RabbitMQ connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish RabbitMQ connection");
                throw;
            }
        }

        private static ConnectionFactory CreateConnectionFactory(IConfiguration configuration)
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest",
                VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",

                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60),

                RequestedChannelMax = 2047,
                RequestedFrameMax = 0,

                Ssl = new SslOption
                {
                    Enabled = bool.Parse(configuration["RabbitMQ:UseSsl"] ?? "false"),
                    ServerName = configuration["RabbitMQ:HostName"] ?? "localhost"
                }
            };

            return factory;
        }

        public async Task PublishAsync<T>(T message, string queueName) where T : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RabbitMQPublisher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(queueName))
                throw new ArgumentException("Queue name cannot be null or empty", nameof(queueName));

            await _semaphore.WaitAsync();
            try
            {
                await PublishMessageInternal(message, queueName);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task PublishMessageInternal<T>(T message, string queueName) where T : class
        {
            var retryCount = 0;
            const int maxRetries = 3;
            var delay = TimeSpan.FromMilliseconds(100);

            while (retryCount <= maxRetries)
            {
                try
                {
                    DeclareQueue(queueName);

                    var messageBody = SerializeMessage(message);
                    var body = Encoding.UTF8.GetBytes(messageBody);

                    var properties = CreateMessageProperties();

                    lock (_lockObject)
                    {
                        _channel.BasicPublish(
                            exchange: "",
                            routingKey: queueName,
                            basicProperties: properties,
                            body: body);
                    }

                    var confirmed = _channel.WaitForConfirms(TimeSpan.FromSeconds(5));
                    if (!confirmed)
                    {
                        throw new InvalidOperationException("Message publish was not confirmed by RabbitMQ");
                    }

                    _logger.LogDebug("Message published successfully to queue {QueueName}. Message type: {MessageType}",
                        queueName, typeof(T).Name);
                    return;
                }
                catch (AlreadyClosedException ex)
                {
                    _logger.LogWarning(ex, "Connection closed, attempting to reconnect. Retry {RetryCount}/{MaxRetries}",
                        retryCount + 1, maxRetries);

                    if (retryCount >= maxRetries)
                        throw;
                }
                catch (BrokerUnreachableException ex)
                {
                    _logger.LogWarning(ex, "Broker unreachable, retrying. Retry {RetryCount}/{MaxRetries}",
                        retryCount + 1, maxRetries);

                    if (retryCount >= maxRetries)
                        throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error publishing message to queue {QueueName}. Retry {RetryCount}/{MaxRetries}",
                        queueName, retryCount + 1, maxRetries);

                    if (retryCount >= maxRetries)
                        throw;
                }

                retryCount++;
                if (retryCount <= maxRetries)
                {
                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                }
            }
        }

        private void DeclareQueue(string queueName)
        {
            try
            {
                lock (_lockObject)
                {
                    _channel.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: new Dictionary<string, object>
                        {
                            {"x-message-ttl", 86400000}, // 24 hours
                            {"x-max-length", 10000} 
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to declare queue {QueueName}", queueName);
                throw;
            }
        }

        private static string SerializeMessage<T>(T message) where T : class
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            return JsonSerializer.Serialize(message, options);
        }

        private IBasicProperties CreateMessageProperties()
        {
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.DeliveryMode = 2; 
            properties.ContentType = "application/json";
            properties.ContentEncoding = "UTF-8";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.MessageId = Guid.NewGuid().ToString();

            return properties;
        }

        public bool IsHealthy()
        {
            try
            {
                return _connection?.IsOpen == true && _channel?.IsOpen == true;
            }
            catch
            {
                return false;
            }
        }

        public async Task PublishToExchangeAsync<T>(T message, string exchangeName, string routingKey = "") where T : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RabbitMQPublisher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(exchangeName))
                throw new ArgumentException("Exchange name cannot be null or empty", nameof(exchangeName));

            await _semaphore.WaitAsync();
            try
            {
                lock (_lockObject)
                {
                    _channel.ExchangeDeclare(
                        exchange: exchangeName,
                        type: ExchangeType.Topic,
                        durable: true,
                        autoDelete: false);
                }

                var messageBody = SerializeMessage(message);
                var body = Encoding.UTF8.GetBytes(messageBody);
                var properties = CreateMessageProperties();

                lock (_lockObject)
                {
                    _channel.BasicPublish(
                        exchange: exchangeName,
                        routingKey: routingKey,
                        basicProperties: properties,
                        body: body);
                }

                var confirmed = _channel.WaitForConfirms(TimeSpan.FromSeconds(5));
                if (!confirmed)
                {
                    throw new InvalidOperationException("Message publish was not confirmed by RabbitMQ");
                }

                _logger.LogDebug("Message published successfully to exchange {ExchangeName} with routing key {RoutingKey}. Message type: {MessageType}",
                    exchangeName, routingKey, typeof(T).Name);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _semaphore?.Wait();

                _channel?.Close();
                _connection?.Close();

                _channel?.Dispose();
                _connection?.Dispose();
                _semaphore?.Dispose();

                _logger.LogInformation("RabbitMQ connection disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing RabbitMQ connection");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}