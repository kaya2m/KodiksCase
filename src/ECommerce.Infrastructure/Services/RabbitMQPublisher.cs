using ECommerce.Core.Interfaces.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

namespace ECommerce.Infrastructure.Services
{
    public class RabbitMQPublisher : IMessagePublisher, IDisposable
    {
        private IConnection? _connection;
        private IModel? _channel;
        private readonly ILogger<RabbitMQPublisher> _logger;
        private readonly IConfiguration _configuration;
        private readonly object _lockObject = new object();
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed = false;
        private bool _isConnected = false;

        public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _semaphore = new SemaphoreSlim(1, 1);

            _logger.LogInformation("RabbitMQ Publisher initialized (connection will be established on first use)");
        }

        private void EnsureConnection()
        {
            if (_isConnected && _connection?.IsOpen == true && _channel?.IsOpen == true)
                return;

            lock (_lockObject)
            {
                if (_isConnected && _connection?.IsOpen == true && _channel?.IsOpen == true)
                    return;

                try
                {
                    CleanupConnection();

                    var factory = CreateConnectionFactory(_configuration);

                    _logger.LogInformation("Attempting to connect to RabbitMQ...");
                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    _channel.ConfirmSelect();

                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _connection.CallbackException += OnCallbackException;
                    _connection.ConnectionBlocked += OnConnectionBlocked;

                    _isConnected = true;
                    _logger.LogInformation("RabbitMQ connection established successfully");
                }
                catch (Exception ex)
                {
                    _isConnected = false;
                    _logger.LogError(ex, "Failed to establish RabbitMQ connection");

                    var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
                    if (environment == "Development")
                    {
                        _logger.LogWarning("RabbitMQ connection failed in Development environment. Messages will be logged instead of queued.");
                        return;
                    }

                    throw;
                }
            }
        }

        private void CleanupConnection()
        {
            try
            {
                if (_channel?.IsOpen == true)
                {
                    _channel.Close();
                }
                _channel?.Dispose();

                if (_connection?.IsOpen == true)
                {
                    _connection.Close();
                }
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during connection cleanup");
            }
            finally
            {
                _channel = null;
                _connection = null;
                _isConnected = false;
            }
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
                EnsureConnection();

                if (!_isConnected)
                {
                    _logger.LogWarning("RabbitMQ not connected. Message will be logged: {Message} to queue {Queue}",
                        JsonSerializer.Serialize(message), queueName);
                    return;
                }

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
                    if (_channel == null || !_channel.IsOpen)
                    {
                        EnsureConnection();
                        if (!_isConnected) return;
                    }

                    DeclareQueue(queueName);

                    var messageBody = SerializeMessage(message);
                    var body = Encoding.UTF8.GetBytes(messageBody);

                    var properties = CreateMessageProperties();

                    lock (_lockObject)
                    {
                        _channel?.BasicPublish(
                            exchange: "",
                            routingKey: queueName,
                            basicProperties: properties,
                            body: body);
                    }

                    var confirmed = _channel?.WaitForConfirms(TimeSpan.FromSeconds(5)) ?? false;
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

                    _isConnected = false;
                    if (retryCount >= maxRetries)
                        throw;
                }
                catch (BrokerUnreachableException ex)
                {
                    _logger.LogWarning(ex, "Broker unreachable, retrying. Retry {RetryCount}/{MaxRetries}",
                        retryCount + 1, maxRetries);

                    _isConnected = false;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError("RabbitMQ broker unreachable after {MaxRetries} attempts. Message logged: {Message}",
                            maxRetries, JsonSerializer.Serialize(message));
                        return;
                    }
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
                    _channel?.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    _logger.LogDebug("Queue {QueueName} declared successfully", queueName);
                }
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason.ReplyCode == 406)
            {
                _logger.LogWarning("Queue {QueueName} exists with different configuration, recovering channel", queueName);
                RecoverChannelAfterError();
            }
            catch (AlreadyClosedException ex)
            {
                _logger.LogWarning(ex, "Channel closed during queue declaration for {QueueName}, recovering", queueName);
                RecoverChannelAfterError();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to declare queue {QueueName}", queueName);
                _isConnected = false;
                throw;
            }
        }

        private void RecoverChannelAfterError()
        {
            try
            {
                if (_connection?.IsOpen == true)
                {
                    try
                    {
                        _channel?.Close();
                    }
                    catch { }

                    _channel?.Dispose();
                    _channel = _connection.CreateModel();
                    _channel.ConfirmSelect();

                    _logger.LogDebug("Channel recovered successfully");
                }
                else
                {
                    _isConnected = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover channel");
                _isConnected = false;
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

                RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
                SocketReadTimeout = TimeSpan.FromSeconds(30),
                SocketWriteTimeout = TimeSpan.FromSeconds(30),

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

        private IBasicProperties? CreateMessageProperties()
        {
            var properties = _channel?.CreateBasicProperties();
            if (properties != null)
            {
                properties.Persistent = true;
                properties.DeliveryMode = 2;
                properties.ContentType = "application/json";
                properties.ContentEncoding = "UTF-8";
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.MessageId = Guid.NewGuid().ToString();
            }
            return properties;
        }

        private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shutdown: {Reason}", e.ReplyText);
            _isConnected = false;
        }

        private void OnCallbackException(object? sender, CallbackExceptionEventArgs e)
        {
            _logger.LogError(e.Exception, "RabbitMQ callback exception");
        }

        private void OnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection blocked: {Reason}", e.Reason);
        }

        public bool IsHealthy()
        {
            try
            {
                return _isConnected && _connection?.IsOpen == true && _channel?.IsOpen == true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _semaphore?.Wait();
                CleanupConnection();
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