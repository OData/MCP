using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for logging, metrics, and health monitoring.
    /// </summary>
    /// <remarks>
    /// Monitoring configuration controls what information is logged, how metrics
    /// are collected, and what health checks are performed for operational visibility.
    /// </remarks>
    public sealed class MonitoringConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or sets the minimum logging level.
        /// </summary>
        /// <value>The minimum level of log messages to record.</value>
        /// <remarks>
        /// Log levels follow standard .NET logging conventions: Trace, Debug, Information,
        /// Warning, Error, Critical. Lower levels include all higher levels.
        /// </remarks>
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// Gets or sets a value indicating whether to enable structured logging.
        /// </summary>
        /// <value><c>true</c> to enable structured logging; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Structured logging outputs log messages in a structured format (like JSON)
        /// that can be easily parsed by log aggregation systems.
        /// </remarks>
        public bool EnableStructuredLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to log request/response details.
        /// </summary>
        /// <value><c>true</c> to log HTTP request and response details; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Request/response logging provides detailed information about HTTP traffic
        /// but can impact performance and may log sensitive information.
        /// </remarks>
        public bool LogRequestResponse { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to log sensitive data.
        /// </summary>
        /// <value><c>true</c> to include sensitive data in logs; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Sensitive data includes authentication tokens, personal information, and other
        /// confidential data. This should be disabled in production environments.
        /// </remarks>
        public bool LogSensitiveData { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to enable performance metrics collection.
        /// </summary>
        /// <value><c>true</c> to collect performance metrics; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Performance metrics include response times, throughput, error rates,
        /// and other operational metrics useful for monitoring and alerting.
        /// </remarks>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable health checks.
        /// </summary>
        /// <value><c>true</c> to enable health check endpoints; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Health checks provide automated monitoring of service health status
        /// and can be used by load balancers and monitoring systems.
        /// </remarks>
        public bool EnableHealthChecks { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable distributed tracing.
        /// </summary>
        /// <value><c>true</c> to enable distributed tracing; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Distributed tracing tracks requests across multiple services and provides
        /// end-to-end visibility in microservice architectures.
        /// </remarks>
        public bool EnableTracing { get; set; } = false;

        /// <summary>
        /// Gets or sets the tracing sampling rate.
        /// </summary>
        /// <value>The percentage of requests to trace (0.0 to 1.0).</value>
        /// <remarks>
        /// Sampling reduces the overhead of tracing by only capturing a percentage
        /// of requests. A value of 1.0 traces all requests.
        /// </remarks>
        public double TracingSamplingRate { get; set; } = 0.1; // 10%

        /// <summary>
        /// Gets or sets the metrics collection interval.
        /// </summary>
        /// <value>The interval between metrics collection cycles.</value>
        /// <remarks>
        /// More frequent collection provides better visibility but increases overhead.
        /// The optimal interval depends on your monitoring requirements.
        /// </remarks>
        public TimeSpan MetricsInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the health check interval.
        /// </summary>
        /// <value>The interval between health check executions.</value>
        /// <remarks>
        /// More frequent health checks provide faster failure detection but
        /// increase system load.
        /// </remarks>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the health check timeout.
        /// </summary>
        /// <value>The maximum time to wait for health check completion.</value>
        /// <remarks>
        /// Health checks that take longer than this timeout will be considered failed.
        /// </remarks>
        public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the OpenTelemetry configuration.
        /// </summary>
        /// <value>Configuration for OpenTelemetry observability.</value>
        /// <remarks>
        /// OpenTelemetry provides standardized observability with metrics, logs,
        /// and traces that can be exported to various monitoring systems.
        /// </remarks>
        public OpenTelemetryConfiguration OpenTelemetry { get; set; } = new();

        /// <summary>
        /// Gets or sets the application insights configuration.
        /// </summary>
        /// <value>Configuration for Azure Application Insights integration.</value>
        /// <remarks>
        /// Application Insights provides comprehensive application performance
        /// monitoring and analytics for Azure-hosted applications.
        /// </remarks>
        public ApplicationInsightsConfiguration ApplicationInsights { get; set; } = new();

        /// <summary>
        /// Gets or sets the custom metric definitions.
        /// </summary>
        /// <value>A collection of custom metrics to collect.</value>
        /// <remarks>
        /// Custom metrics allow tracking application-specific measurements
        /// beyond the standard performance metrics.
        /// </remarks>
        public List<MetricDefinition> CustomMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the log filters.
        /// </summary>
        /// <value>A collection of filters to apply to log messages.</value>
        /// <remarks>
        /// Log filters can suppress noisy log messages or enhance logging
        /// for specific components or scenarios.
        /// </remarks>
        public List<LogFilter> LogFilters { get; set; } = new();

        /// <summary>
        /// Gets or sets the alerting configuration.
        /// </summary>
        /// <value>Configuration for automated alerts based on metrics and logs.</value>
        /// <remarks>
        /// Alerting configuration defines when and how to notify operations teams
        /// about system issues or performance degradations.
        /// </remarks>
        public AlertingConfiguration Alerting { get; set; } = new();

        /// <summary>
        /// Gets or sets custom monitoring properties.
        /// </summary>
        /// <value>A dictionary of custom monitoring configuration values.</value>
        /// <remarks>
        /// Custom properties allow extending the configuration with monitoring system-specific
        /// settings that don't fit into the standard configuration properties.
        /// </remarks>
        public Dictionary<string, object> CustomProperties { get; set; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoringConfiguration"/> class.
        /// </summary>
        public MonitoringConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the monitoring configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(LogLevel))
            {
                errors.Add("LogLevel cannot be null or whitespace");
            }
            else
            {
                var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
                if (!validLevels.Contains(LogLevel, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"LogLevel must be one of: {string.Join(", ", validLevels)}");
                }
            }

            if (TracingSamplingRate < 0.0 || TracingSamplingRate > 1.0)
            {
                errors.Add("TracingSamplingRate must be between 0.0 and 1.0");
            }

            if (MetricsInterval <= TimeSpan.Zero)
            {
                errors.Add("MetricsInterval must be greater than zero");
            }

            if (HealthCheckInterval <= TimeSpan.Zero)
            {
                errors.Add("HealthCheckInterval must be greater than zero");
            }

            if (HealthCheckTimeout <= TimeSpan.Zero)
            {
                errors.Add("HealthCheckTimeout must be greater than zero");
            }

            // Validate OpenTelemetry configuration
            var otelErrors = OpenTelemetry.Validate();
            errors.AddRange(otelErrors.Select(e => $"OpenTelemetry: {e}"));

            // Validate Application Insights configuration
            var appInsightsErrors = ApplicationInsights.Validate();
            errors.AddRange(appInsightsErrors.Select(e => $"ApplicationInsights: {e}"));

            // Validate custom metrics
            for (int i = 0; i < CustomMetrics.Count; i++)
            {
                var metricErrors = CustomMetrics[i].Validate();
                errors.AddRange(metricErrors.Select(e => $"CustomMetrics[{i}]: {e}"));
            }

            // Validate log filters
            for (int i = 0; i < LogFilters.Count; i++)
            {
                var filterErrors = LogFilters[i].Validate();
                errors.AddRange(filterErrors.Select(e => $"LogFilters[{i}]: {e}"));
            }

            // Validate alerting configuration
            var alertingErrors = Alerting.Validate();
            errors.AddRange(alertingErrors.Select(e => $"Alerting: {e}"));

            return errors;
        }

        /// <summary>
        /// Creates a configuration optimized for development environments.
        /// </summary>
        /// <returns>A monitoring configuration suitable for development.</returns>
        public static MonitoringConfiguration ForDevelopment()
        {
            return new MonitoringConfiguration
            {
                LogLevel = "Debug",
                EnableStructuredLogging = true,
                LogRequestResponse = true,
                LogSensitiveData = true,
                EnableMetrics = true,
                EnableHealthChecks = true,
                EnableTracing = true,
                TracingSamplingRate = 1.0, // Trace everything in development
                MetricsInterval = TimeSpan.FromSeconds(30),
                HealthCheckInterval = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// Creates a configuration optimized for production environments.
        /// </summary>
        /// <returns>A monitoring configuration suitable for production.</returns>
        public static MonitoringConfiguration ForProduction()
        {
            return new MonitoringConfiguration
            {
                LogLevel = "Warning",
                EnableStructuredLogging = true,
                LogRequestResponse = false,
                LogSensitiveData = false,
                EnableMetrics = true,
                EnableHealthChecks = true,
                EnableTracing = true,
                TracingSamplingRate = 0.01, // 1% sampling in production
                MetricsInterval = TimeSpan.FromMinutes(1),
                HealthCheckInterval = TimeSpan.FromMinutes(1),
                OpenTelemetry = new OpenTelemetryConfiguration { Enabled = true },
                Alerting = new AlertingConfiguration { Enabled = true }
            };
        }

        /// <summary>
        /// Creates a minimal monitoring configuration.
        /// </summary>
        /// <returns>A monitoring configuration with minimal overhead.</returns>
        public static MonitoringConfiguration Minimal()
        {
            return new MonitoringConfiguration
            {
                LogLevel = "Error",
                EnableStructuredLogging = false,
                LogRequestResponse = false,
                LogSensitiveData = false,
                EnableMetrics = false,
                EnableHealthChecks = true,
                EnableTracing = false,
                HealthCheckInterval = TimeSpan.FromMinutes(5)
            };
        }

        /// <summary>
        /// Adds a custom metric definition.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="type">The metric type.</param>
        /// <param name="description">The metric description.</param>
        /// <param name="unit">The metric unit.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
        public void AddCustomMetric(string name, MetricType type, string description, string? unit = null)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
#else
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Metric name cannot be null or whitespace.", nameof(name));
            }
#endif

            CustomMetrics.Add(new MetricDefinition
            {
                Name = name,
                Type = type,
                Description = description ?? string.Empty,
                Unit = unit
            });
        }

        /// <summary>
        /// Adds a log filter.
        /// </summary>
        /// <param name="category">The log category to filter.</param>
        /// <param name="level">The minimum log level for the category.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="category"/> is null or whitespace.</exception>
        public void AddLogFilter(string category, string level)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(category);
#else
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("Category cannot be null or whitespace.", nameof(category));
            }
#endif

            LogFilters.Add(new LogFilter
            {
                Category = category,
                Level = level ?? LogLevel
            });
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public MonitoringConfiguration Clone()
        {
            return new MonitoringConfiguration
            {
                LogLevel = LogLevel,
                EnableStructuredLogging = EnableStructuredLogging,
                LogRequestResponse = LogRequestResponse,
                LogSensitiveData = LogSensitiveData,
                EnableMetrics = EnableMetrics,
                EnableHealthChecks = EnableHealthChecks,
                EnableTracing = EnableTracing,
                TracingSamplingRate = TracingSamplingRate,
                MetricsInterval = MetricsInterval,
                HealthCheckInterval = HealthCheckInterval,
                HealthCheckTimeout = HealthCheckTimeout,
                OpenTelemetry = OpenTelemetry.Clone(),
                ApplicationInsights = ApplicationInsights.Clone(),
                CustomMetrics = CustomMetrics.Select(m => m.Clone()).ToList(),
                LogFilters = LogFilters.Select(f => f.Clone()).ToList(),
                Alerting = Alerting.Clone(),
                CustomProperties = new Dictionary<string, object>(CustomProperties)
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public void MergeWith(MonitoringConfiguration other)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(other);
#else
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }
#endif

            if (!string.IsNullOrWhiteSpace(other.LogLevel)) LogLevel = other.LogLevel;
            
            EnableStructuredLogging = other.EnableStructuredLogging;
            LogRequestResponse = other.LogRequestResponse;
            LogSensitiveData = other.LogSensitiveData;
            EnableMetrics = other.EnableMetrics;
            EnableHealthChecks = other.EnableHealthChecks;
            EnableTracing = other.EnableTracing;
            TracingSamplingRate = other.TracingSamplingRate;
            MetricsInterval = other.MetricsInterval;
            HealthCheckInterval = other.HealthCheckInterval;
            HealthCheckTimeout = other.HealthCheckTimeout;

            OpenTelemetry.MergeWith(other.OpenTelemetry);
            ApplicationInsights.MergeWith(other.ApplicationInsights);
            Alerting.MergeWith(other.Alerting);

            // Replace collections entirely
            CustomMetrics = other.CustomMetrics.Select(m => m.Clone()).ToList();
            LogFilters = other.LogFilters.Select(f => f.Clone()).ToList();

            // Merge custom properties
            foreach (var kvp in other.CustomProperties)
            {
                CustomProperties[kvp.Key] = kvp.Value;
            }
        }

        #endregion
    }

    /// <summary>
    /// OpenTelemetry configuration for observability.
    /// </summary>
    public sealed class OpenTelemetryConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether OpenTelemetry is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the OTLP endpoint for exporting telemetry data.
        /// </summary>
        public string? OtlpEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the service name for telemetry data.
        /// </summary>
        public string ServiceName { get; set; } = "odata-mcp-server";

        /// <summary>
        /// Gets or sets the service version for telemetry data.
        /// </summary>
        public string ServiceVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets additional resource attributes.
        /// </summary>
        public Dictionary<string, string> ResourceAttributes { get; set; } = new();

        /// <summary>
        /// Validates the OpenTelemetry configuration.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (Enabled && string.IsNullOrWhiteSpace(ServiceName))
            {
                errors.Add("ServiceName is required when OpenTelemetry is enabled");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public OpenTelemetryConfiguration Clone()
        {
            return new OpenTelemetryConfiguration
            {
                Enabled = Enabled,
                OtlpEndpoint = OtlpEndpoint,
                ServiceName = ServiceName,
                ServiceVersion = ServiceVersion,
                ResourceAttributes = new Dictionary<string, string>(ResourceAttributes)
            };
        }

        /// <summary>
        /// Merges another configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge.</param>
        public void MergeWith(OpenTelemetryConfiguration other)
        {
            if (other is null) return;

            Enabled = other.Enabled;
            if (!string.IsNullOrWhiteSpace(other.OtlpEndpoint)) OtlpEndpoint = other.OtlpEndpoint;
            if (!string.IsNullOrWhiteSpace(other.ServiceName)) ServiceName = other.ServiceName;
            if (!string.IsNullOrWhiteSpace(other.ServiceVersion)) ServiceVersion = other.ServiceVersion;

            foreach (var kvp in other.ResourceAttributes)
            {
                ResourceAttributes[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Azure Application Insights configuration.
    /// </summary>
    public sealed class ApplicationInsightsConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether Application Insights is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the Application Insights connection string.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the instrumentation key (legacy).
        /// </summary>
        public string? InstrumentationKey { get; set; }

        /// <summary>
        /// Gets or sets the sampling percentage.
        /// </summary>
        public double SamplingPercentage { get; set; } = 100.0;

        /// <summary>
        /// Validates the Application Insights configuration.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (Enabled && string.IsNullOrWhiteSpace(ConnectionString) && string.IsNullOrWhiteSpace(InstrumentationKey))
            {
                errors.Add("ConnectionString or InstrumentationKey is required when Application Insights is enabled");
            }

            if (SamplingPercentage < 0.0 || SamplingPercentage > 100.0)
            {
                errors.Add("SamplingPercentage must be between 0.0 and 100.0");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public ApplicationInsightsConfiguration Clone()
        {
            return new ApplicationInsightsConfiguration
            {
                Enabled = Enabled,
                ConnectionString = ConnectionString,
                InstrumentationKey = InstrumentationKey,
                SamplingPercentage = SamplingPercentage
            };
        }

        /// <summary>
        /// Merges another configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge.</param>
        public void MergeWith(ApplicationInsightsConfiguration other)
        {
            if (other is null) return;

            Enabled = other.Enabled;
            if (!string.IsNullOrWhiteSpace(other.ConnectionString)) ConnectionString = other.ConnectionString;
            if (!string.IsNullOrWhiteSpace(other.InstrumentationKey)) InstrumentationKey = other.InstrumentationKey;
            SamplingPercentage = other.SamplingPercentage;
        }
    }

    /// <summary>
    /// Custom metric definition.
    /// </summary>
    public sealed class MetricDefinition
    {
        /// <summary>
        /// Gets or sets the metric name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the metric type.
        /// </summary>
        public MetricType Type { get; set; } = MetricType.Counter;

        /// <summary>
        /// Gets or sets the metric description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the metric unit.
        /// </summary>
        public string? Unit { get; set; }

        /// <summary>
        /// Gets or sets the metric tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>
        /// Validates the metric definition.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Name)) errors.Add("Name is required");
            return errors;
        }

        /// <summary>
        /// Creates a copy of this definition.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public MetricDefinition Clone()
        {
            return new MetricDefinition
            {
                Name = Name,
                Type = Type,
                Description = Description,
                Unit = Unit,
                Tags = new Dictionary<string, string>(Tags)
            };
        }
    }

    /// <summary>
    /// Log filter configuration.
    /// </summary>
    public sealed class LogFilter
    {
        /// <summary>
        /// Gets or sets the log category.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the minimum log level.
        /// </summary>
        public string Level { get; set; } = "Information";

        /// <summary>
        /// Validates the log filter.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Category)) errors.Add("Category is required");
            if (string.IsNullOrWhiteSpace(Level)) errors.Add("Level is required");
            return errors;
        }

        /// <summary>
        /// Creates a copy of this filter.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public LogFilter Clone()
        {
            return new LogFilter { Category = Category, Level = Level };
        }
    }

    /// <summary>
    /// Alerting configuration.
    /// </summary>
    public sealed class AlertingConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether alerting is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the alert rules.
        /// </summary>
        public List<AlertRule> Rules { get; set; } = new();

        /// <summary>
        /// Validates the alerting configuration.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();
            // Add validation logic as needed
            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public AlertingConfiguration Clone()
        {
            return new AlertingConfiguration
            {
                Enabled = Enabled,
                Rules = Rules.Select(r => r.Clone()).ToList()
            };
        }

        /// <summary>
        /// Merges another configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge.</param>
        public void MergeWith(AlertingConfiguration other)
        {
            if (other is null) return;

            Enabled = other.Enabled;
            Rules = other.Rules.Select(r => r.Clone()).ToList();
        }
    }

    /// <summary>
    /// Alert rule definition.
    /// </summary>
    public sealed class AlertRule
    {
        /// <summary>
        /// Gets or sets the rule name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the metric to monitor.
        /// </summary>
        public string Metric { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the threshold value.
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Gets or sets the comparison operator.
        /// </summary>
        public string Operator { get; set; } = "GreaterThan";

        /// <summary>
        /// Creates a copy of this rule.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public AlertRule Clone()
        {
            return new AlertRule
            {
                Name = Name,
                Metric = Metric,
                Threshold = Threshold,
                Operator = Operator
            };
        }
    }

    /// <summary>
    /// Defines the metric types.
    /// </summary>
    public enum MetricType
    {
        /// <summary>
        /// Counter metric that only increases.
        /// </summary>
        Counter,

        /// <summary>
        /// Gauge metric that can increase or decrease.
        /// </summary>
        Gauge,

        /// <summary>
        /// Histogram metric for measuring distributions.
        /// </summary>
        Histogram,

        /// <summary>
        /// Summary metric with quantiles.
        /// </summary>
        Summary
    }
}