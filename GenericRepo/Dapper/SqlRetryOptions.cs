using System;
using Polly;

namespace GenericRepo.Dapper;

public static partial class SqlRetryHelper
{
    public sealed class SqlRetryOptions
    {
        public int MaxRetryAttempts { get; init; } = 3;
        public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(250);
        public DelayBackoffType Backoff { get; init; } = DelayBackoffType.Exponential;
        public bool UseJitter { get; init; } = true;
        public int CommandTimeoutSeconds { get; init; } = 30;
    }
}
