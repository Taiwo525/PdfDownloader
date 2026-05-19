using System.Net;
using PdfDownloader.Core;
using Polly;
using Polly.Retry;
using Serilog;

namespace PdfDownloader.Infrastructure;

public sealed class ResiliencePipelineFactory : IResiliencePipelineFactory
{
    public ResiliencePipeline<HttpResponseMessage> Create()
    {
        var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(r => IsRetryableStatusCode(r.StatusCode)),
            OnRetry = args =>
            {
                Log.Warning(
                    "Retry {RetryAttempt} after {Delay}. Reason: {Reason}",
                    args.AttemptNumber,
                    args.RetryDelay,
                    args.Outcome.Exception?.Message ?? $"HTTP {(int)args.Outcome.Result!.StatusCode}");

                return ValueTask.CompletedTask;
            }
        };

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retryOptions)
            .Build();
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code == 429 || code >= 500;
    }
}
