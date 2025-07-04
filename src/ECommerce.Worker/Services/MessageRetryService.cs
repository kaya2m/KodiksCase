using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ECommerce.Worker.Services;

public class MessageRetryService
{
    private readonly ILogger<MessageRetryService> _logger;
    private readonly ConcurrentDictionary<string, int> _retryCount = new();
    private const int MaxRetryAttempts = 3;

    public MessageRetryService(ILogger<MessageRetryService> logger)
    {
        _logger = logger;
    }

    public bool ShouldRetry(string messageId, Exception exception)
    {
        var currentRetries = _retryCount.AddOrUpdate(messageId, 1, (key, value) => value + 1);

        _logger.LogWarning(exception,
            "Message {MessageId} failed processing. Attempt {Attempt}/{MaxAttempts}",
            messageId, currentRetries, MaxRetryAttempts);

        if (currentRetries >= MaxRetryAttempts)
        {
            _logger.LogError("Message {MessageId} exceeded maximum retry attempts. Moving to dead letter queue",
                messageId);
            _retryCount.TryRemove(messageId, out _);
            return false;
        }

        return true;
    }

    public TimeSpan GetRetryDelay(int attemptNumber)
    {
        return TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
    }

    public void ClearRetryCount(string messageId)
    {
        _retryCount.TryRemove(messageId, out _);
    }
}