using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Configuration options for retry policies in authentication operations.
    /// </summary>
    /// <remarks>
    /// Retry policies help handle transient failures in authentication and token
    /// delegation operations, such as network timeouts, temporary service
    /// unavailability, or rate limiting from authorization servers.
    /// </remarks>
    public sealed class RetryPolicyOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether retry is enabled.
        /// </summary>
        /// <value><c>true</c> if retry is enabled; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When disabled, failed operations will not be retried and will fail
        /// immediately. Enabling retries can improve reliability but may
        /// increase latency for operations that ultimately fail.
        /// </remarks>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// </summary>
        /// <value>The maximum number of times to retry a failed operation.</value>
        /// <remarks>
        /// This count does not include the initial attempt. For example, a value
        /// of 3 means the operation will be attempted up to 4 times total.
        /// Higher values provide more resilience but may cause longer delays.
        /// </remarks>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the base delay between retry attempts.
        /// </summary>
        /// <value>The initial delay before the first retry attempt.</value>
        /// <remarks>
        /// The actual delay may be modified by the backoff strategy.
        /// This value should be chosen based on the expected recovery time
        /// for transient failures.
        /// </remarks>
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum delay between retry attempts.
        /// </summary>
        /// <value>The maximum time to wait before a retry attempt.</value>
        /// <remarks>
        /// This prevents exponential backoff from creating extremely long
        /// delays. The actual delay will be capped at this value regardless
        /// of the backoff calculation.
        /// </remarks>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the backoff strategy for calculating retry delays.
        /// </summary>
        /// <value>The strategy used to calculate delays between retry attempts.</value>
        /// <remarks>
        /// Different backoff strategies provide different trade-offs between
        /// recovery speed and load on the target service. Exponential backoff
        /// is generally recommended for most scenarios.
        /// </remarks>
        public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.ExponentialWithJitter;

        /// <summary>
        /// Gets or sets the jitter factor for randomizing retry delays.
        /// </summary>
        /// <value>A value between 0.0 and 1.0 that controls the amount of randomization applied to delays.</value>
        /// <remarks>
        /// Jitter helps prevent the "thundering herd" problem when multiple
        /// clients retry simultaneously. A value of 0.0 disables jitter, while
        /// 1.0 allows delays to vary by up to 100% of the calculated value.
        /// </remarks>
        public double JitterFactor { get; set; } = 0.2;

        /// <summary>
        /// Gets or sets the HTTP status codes that should trigger retries.
        /// </summary>
        /// <value>A collection of HTTP status codes that indicate retryable failures.</value>
        /// <remarks>
        /// Only failures with these status codes will be retried. Other status
        /// codes will cause the operation to fail immediately. Common retryable
        /// codes include 429 (Too Many Requests), 502 (Bad Gateway), and
        /// 503 (Service Unavailable).
        /// </remarks>
        public HashSet<int> RetryableStatusCodes { get; set; } =
        [
            408, // Request Timeout
            429, // Too Many Requests
            500, // Internal Server Error
            502, // Bad Gateway
            503, // Service Unavailable
            504  // Gateway Timeout
        ];

        /// <summary>
        /// Gets or sets the exception types that should trigger retries.
        /// </summary>
        /// <value>A collection of exception type names that indicate retryable failures.</value>
        /// <remarks>
        /// Exceptions of these types will trigger retries. The type names should
        /// be the full type name including namespace. This is typically used for
        /// network-related exceptions like timeouts and connection failures.
        /// </remarks>
        public HashSet<string> RetryableExceptionTypes { get; set; } =
        [
            "System.Net.Http.HttpRequestException",
            "System.Threading.Tasks.TaskCanceledException",
            "System.Net.Sockets.SocketException",
            "System.TimeoutException"
        ];

        /// <summary>
        /// Gets or sets a value indicating whether to use circuit breaker pattern.
        /// </summary>
        /// <value><c>true</c> if circuit breaker should be used; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// The circuit breaker pattern prevents cascading failures by temporarily
        /// stopping retries when a service is consistently failing. This can
        /// improve overall system stability during outages.
        /// </remarks>
        public bool UseCircuitBreaker { get; set; } = false;

        /// <summary>
        /// Gets or sets the circuit breaker failure threshold.
        /// </summary>
        /// <value>The number of consecutive failures that will trip the circuit breaker.</value>
        /// <remarks>
        /// Once this many consecutive failures occur, the circuit breaker will
        /// "open" and prevent further attempts for a period of time. This helps
        /// avoid overwhelming a failing service.
        /// </remarks>
        public int CircuitBreakerFailureThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets the circuit breaker recovery timeout.
        /// </summary>
        /// <value>The time to wait before attempting to close an open circuit breaker.</value>
        /// <remarks>
        /// After the circuit breaker opens, it will remain open for this duration
        /// before allowing a test request to check if the service has recovered.
        /// </remarks>
        public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(1);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryPolicyOptions"/> class.
        /// </summary>
        public RetryPolicyOptions()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Calculates the delay for a specific retry attempt.
        /// </summary>
        /// <param name="attemptNumber">The retry attempt number (starting from 1).</param>
        /// <returns>The delay to wait before the retry attempt.</returns>
        public TimeSpan CalculateDelay(int attemptNumber)
        {
            if (attemptNumber <= 0)
            {
                return TimeSpan.Zero;
            }

            var delay = BackoffStrategy switch
            {
                BackoffStrategy.Fixed => BaseDelay,
                BackoffStrategy.Linear => TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * attemptNumber),
                BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1)),
                BackoffStrategy.ExponentialWithJitter => CalculateExponentialWithJitter(attemptNumber),
                _ => BaseDelay
            };

            // Apply maximum delay cap
            if (delay > MaxDelay)
            {
                delay = MaxDelay;
            }

            return delay;
        }

        /// <summary>
        /// Determines whether an exception should trigger a retry.
        /// </summary>
        /// <param name="exception">The exception to check.</param>
        /// <returns><c>true</c> if the exception should trigger a retry; otherwise, <c>false</c>.</returns>
        public bool ShouldRetry(Exception exception)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(exception);
#else
            if (exception is null)
            {
                throw new ArgumentNullException(nameof(exception));
            }
#endif

            var exceptionType = exception.GetType().FullName;
            return exceptionType is not null && RetryableExceptionTypes.Contains(exceptionType);
        }

        /// <summary>
        /// Determines whether an HTTP status code should trigger a retry.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to check.</param>
        /// <returns><c>true</c> if the status code should trigger a retry; otherwise, <c>false</c>.</returns>
        public bool ShouldRetry(int statusCode)
        {
            return RetryableStatusCodes.Contains(statusCode);
        }

        /// <summary>
        /// Validates the retry policy options for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the options are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (Enabled)
            {
                if (MaxAttempts < 0)
                {
                    errors.Add("MaxAttempts cannot be negative.");
                }

                if (BaseDelay < TimeSpan.Zero)
                {
                    errors.Add("BaseDelay cannot be negative.");
                }

                if (MaxDelay < TimeSpan.Zero)
                {
                    errors.Add("MaxDelay cannot be negative.");
                }

                if (MaxDelay < BaseDelay)
                {
                    errors.Add("MaxDelay cannot be less than BaseDelay.");
                }

                if (JitterFactor < 0.0 || JitterFactor > 1.0)
                {
                    errors.Add("JitterFactor must be between 0.0 and 1.0.");
                }

                if (UseCircuitBreaker)
                {
                    if (CircuitBreakerFailureThreshold <= 0)
                    {
                        errors.Add("CircuitBreakerFailureThreshold must be greater than zero.");
                    }

                    if (CircuitBreakerTimeout <= TimeSpan.Zero)
                    {
                        errors.Add("CircuitBreakerTimeout must be greater than zero.");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the retry policy options.
        /// </summary>
        /// <returns>A summary of the retry policy configuration.</returns>
        public override string ToString()
        {
            if (!Enabled)
            {
                return "Retry Policy: Disabled";
            }

            return $"Retry Policy: {MaxAttempts} attempts, {BackoffStrategy} backoff, Circuit Breaker: {UseCircuitBreaker}";
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Calculates exponential backoff delay with jitter.
        /// </summary>
        /// <param name="attemptNumber">The retry attempt number.</param>
        /// <returns>The calculated delay with jitter applied.</returns>
        internal TimeSpan CalculateExponentialWithJitter(int attemptNumber)
        {
            var exponentialDelay = BaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1);
            
            if (JitterFactor > 0.0)
            {
                var random = new Random();
                var jitterRange = exponentialDelay * JitterFactor;
                var jitter = (random.NextDouble() - 0.5) * 2 * jitterRange; // Random value between -jitterRange and +jitterRange
                exponentialDelay += jitter;
            }

            return TimeSpan.FromMilliseconds(Math.Max(0, exponentialDelay));
        }

        #endregion

    }

}
