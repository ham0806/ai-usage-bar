namespace AiUsageBar.Core.Providers;

public class AuthRequiredException : Exception
{
    public AuthRequiredException(string message) : base(message)
    {
    }
}

public class RateLimitedException : Exception
{
    public double? RetryAfter { get; }

    public RateLimitedException(double? retryAfter = null) : base("rate limited")
    {
        RetryAfter = retryAfter;
    }
}

public class FetchException : Exception
{
    public FetchException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
