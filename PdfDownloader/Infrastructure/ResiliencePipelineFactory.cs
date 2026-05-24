using System.Net;
using PdfDownloader.Core;
using PdfDownloader.Models;
using Polly;
using Polly.Retry;
using Serilog;

namespace PdfDownloader.Infrastructure;

/// <summary>
/// Factory for creating resilience pipelines with retry logic for HTTP requests.
/// </summary>
public sealed class ResiliencePipelineFactory(HttpSettings settings, ILogger logger) : IResiliencePipelineFactory
{
    public ResiliencePipeline<HttpResponseMessage> Create()
    {
        var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = settings.MaxRetryAttempts,
            Delay = TimeSpan.FromSeconds(settings.RetryDelaySeconds),
            BackoffType = settings.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
            UseJitter = settings.UseJitter,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(r => IsRetryableStatusCode(r.StatusCode)),
            OnRetry = args =>
            {
                var reason = args.Outcome.Exception?.Message ?? $"HTTP {(int)args.Outcome.Result!.StatusCode} ({args.Outcome.Result.ReasonPhrase})";
                
                logger.Warning(
                    "Retry attempt {RetryAttempt} of {MaxRetryAttempts} after {Delay:F2}s delay. Reason: {Reason}",
                    args.AttemptNumber,
                    settings.MaxRetryAttempts,
                    args.RetryDelay.TotalSeconds,
                    reason);

                return ValueTask.CompletedTask;
            }
        };

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retryOptions)
            .Build();
    }

    /// <summary>
    /// Determines if an HTTP status code should trigger a retry.
    /// Retries on: 408 (Request Timeout), 429 (Too Many Requests), and 5xx (Server Errors).
    /// </summary>
    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code == 429 || code >= 500;
    }
}
