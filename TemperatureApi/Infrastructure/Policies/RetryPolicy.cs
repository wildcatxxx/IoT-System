using Polly;
using Polly.Retry;

namespace TemperatureApi.Infrastructure.Policies;

public static class RetryPolicies
{
    public static AsyncRetryPolicy GetDefaultPolicy()
    {
        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}