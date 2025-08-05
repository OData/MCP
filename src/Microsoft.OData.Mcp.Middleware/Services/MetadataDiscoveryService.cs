using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Middleware.Configuration;

namespace Microsoft.OData.Mcp.Middleware.Services
{
    /// <summary>
    /// Service for discovering and managing OData metadata within the middleware.
    /// </summary>
    /// <remarks>
    /// This service handles automatic discovery of OData metadata from the host application,
    /// caching of parsed models, and notifications when metadata changes.
    /// </remarks>
    public sealed class MetadataDiscoveryService : BackgroundService, IMetadataDiscoveryService
    {
        #region Fields

        private readonly McpMiddlewareOptions _options;
        private readonly CsdlParser _parser;
        private readonly IHttpClientFactory? _httpClientFactory;
        private readonly ILogger<MetadataDiscoveryService> _logger;
        private readonly object _lock = new();

        private EdmModel? _currentModel;
        private DateTime? _lastUpdated;
        private bool _isDiscovering;
        private int _discoveryAttempts;
        private Timer? _refreshTimer;

        #endregion

        #region Events

        /// <summary>
        /// Event raised when metadata is discovered or updated.
        /// </summary>
        public event EventHandler<MetadataDiscoveredEventArgs>? MetadataDiscovered;

        /// <summary>
        /// Event raised when metadata discovery fails.
        /// </summary>
        public event EventHandler<MetadataDiscoveryFailedEventArgs>? MetadataDiscoveryFailed;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current OData model, if available.
        /// </summary>
        /// <value>The current OData model, or null if not yet discovered.</value>
        public EdmModel? CurrentModel
        {
            get
            {
                lock (_lock)
                {
                    return _currentModel;
                }
            }
        }

        /// <summary>
        /// Gets the timestamp when the current model was last updated.
        /// </summary>
        /// <value>The UTC timestamp of the last model update, or null if no model is available.</value>
        public DateTime? LastUpdated
        {
            get
            {
                lock (_lock)
                {
                    return _lastUpdated;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether metadata discovery is currently in progress.
        /// </summary>
        /// <value><c>true</c> if discovery is in progress; otherwise, <c>false</c>.</value>
        public bool IsDiscovering
        {
            get
            {
                lock (_lock)
                {
                    return _isDiscovering;
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataDiscoveryService"/> class.
        /// </summary>
        /// <param name="options">The middleware options.</param>
        /// <param name="parser">The CSDL parser.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="httpClientFactory">The HTTP client factory (optional).</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public MetadataDiscoveryService(
            IOptions<McpMiddlewareOptions> options,
            CsdlParser parser,
            ILogger<MetadataDiscoveryService> logger,
            IHttpClientFactory? httpClientFactory = null)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(parser);
            ArgumentNullException.ThrowIfNull(logger);
#else
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (parser is null)
            {
                throw new ArgumentNullException(nameof(parser));
            }
            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
#endif

            _options = options.Value;
            _parser = parser;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually triggers metadata discovery.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous discovery operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service is not started.</exception>
        public async Task DiscoverMetadataAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                _logger.LogDebug("Metadata discovery skipped - middleware is disabled");
                return;
            }

            if (!_options.AutoDiscoverMetadata)
            {
                _logger.LogDebug("Metadata discovery skipped - auto-discovery is disabled");
                return;
            }

            lock (_lock)
            {
                if (_isDiscovering)
                {
                    _logger.LogDebug("Metadata discovery already in progress, skipping");
                    return;
                }
                _isDiscovering = true;
            }

            try
            {
                _logger.LogInformation("Starting metadata discovery attempt {Attempt}", _discoveryAttempts + 1);

                var metadata = await FetchMetadataAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(metadata))
                {
                    var model = _parser.ParseFromString(metadata);
                    SetModelInternal(model, "Discovery", isUpdate: _currentModel is not null);
                    
                    _discoveryAttempts = 0; // Reset on success
                    _logger.LogInformation("Metadata discovery completed successfully");
                }
                else
                {
                    throw new InvalidOperationException("Empty metadata received from discovery endpoint");
                }
            }
            catch (Exception ex)
            {
                _discoveryAttempts++;
                _logger.LogError(ex, "Metadata discovery failed (attempt {Attempt})", _discoveryAttempts);

                var willRetry = _discoveryAttempts < 3; // Max 3 attempts
                OnMetadataDiscoveryFailed(new MetadataDiscoveryFailedEventArgs
                {
                    Exception = ex,
                    Source = GetMetadataUrl(),
                    RetryAttempt = _discoveryAttempts,
                    WillRetry = willRetry,
                    Context = new Dictionary<string, object>
                    {
                        ["DiscoveryAttempts"] = _discoveryAttempts,
                        ["MetadataUrl"] = GetMetadataUrl()
                    }
                });

                if (!willRetry)
                {
                    _logger.LogWarning("Maximum discovery attempts reached, stopping automatic discovery");
                }
            }
            finally
            {
                lock (_lock)
                {
                    _isDiscovering = false;
                }
            }
        }

        /// <summary>
        /// Manually sets the OData model.
        /// </summary>
        /// <param name="model">The OData model to set.</param>
        /// <param name="source">The source of the model (e.g., "Manual", "Discovery").</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public void SetModel(EdmModel model, string source = "Manual")
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(model);
#else
            if (model is null)
            {
                throw new ArgumentNullException(nameof(model));
            }
#endif

            SetModelInternal(model, source, isUpdate: _currentModel is not null);
        }

        /// <summary>
        /// Clears the current model and forces re-discovery.
        /// </summary>
        public void ClearModel()
        {
            lock (_lock)
            {
                _currentModel = null;
                _lastUpdated = null;
                _discoveryAttempts = 0;
            }

            _logger.LogInformation("Current model cleared, will re-discover on next attempt");

            // Trigger immediate discovery if auto-discovery is enabled
            if (_options.AutoDiscoverMetadata)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await DiscoverMetadataAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to re-discover metadata after clearing model");
                    }
                });
            }
        }

        /// <summary>
        /// Validates the current model for completeness and correctness.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the model is valid.</returns>
        public IEnumerable<string> ValidateCurrentModel()
        {
            var model = CurrentModel;
            if (model is null)
            {
                return new[] { "No model is currently available" };
            }

            var errors = new List<string>();

            // Basic model validation
            if (model.EntityTypes.Count == 0)
            {
                errors.Add("Model contains no entity types");
            }

            if (model.EntityContainer is null)
            {
                errors.Add("Model has no entity container");
            }
            else if (model.EntityContainer.EntitySets.Count == 0)
            {
                errors.Add("Entity container has no entity sets");
            }

            // Validate entity relationships
            foreach (var entityType in model.EntityTypes)
            {
                foreach (var navProperty in entityType.NavigationProperties)
                {
                    if (string.IsNullOrWhiteSpace(navProperty.Type))
                    {
                        errors.Add($"Navigation property '{navProperty.Name}' in entity '{entityType.Name}' has no type");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Gets metadata statistics for monitoring and diagnostics.
        /// </summary>
        /// <returns>A dictionary containing metadata statistics.</returns>
        public Dictionary<string, object> GetStatistics()
        {
            var model = CurrentModel;
            var lastUpdated = LastUpdated;

            var stats = new Dictionary<string, object>
            {
                ["HasModel"] = model is not null,
                ["LastUpdated"] = lastUpdated?.ToString("O") ?? "Never",
                ["IsDiscovering"] = IsDiscovering,
                ["DiscoveryAttempts"] = _discoveryAttempts,
                ["AutoDiscoveryEnabled"] = _options.AutoDiscoverMetadata,
                ["RefreshInterval"] = _options.MetadataRefreshInterval.ToString()
            };

            if (model is not null)
            {
                stats["EntityTypeCount"] = model.EntityTypes.Count;
                stats["ComplexTypeCount"] = model.ComplexTypes.Count;
                stats["EntitySetCount"] = model.EntityContainer?.EntitySets.Count ?? 0;
                stats["FunctionCount"] = model.Functions.Count;
                stats["ActionCount"] = model.Actions.Count;

                var totalProperties = model.EntityTypes.SelectMany(et => et.Properties).Count();
                var totalNavProperties = model.EntityTypes.SelectMany(et => et.NavigationProperties).Count();
                stats["TotalProperties"] = totalProperties;
                stats["TotalNavigationProperties"] = totalNavProperties;

                if (lastUpdated.HasValue)
                {
                    stats["ModelAge"] = (DateTime.UtcNow - lastUpdated.Value).ToString();
                }
            }

            return stats;
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Executes the background service.
        /// </summary>
        /// <param name="stoppingToken">The cancellation token that signals when the service should stop.</param>
        /// <returns>A task that represents the background execution.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled || !_options.AutoDiscoverMetadata)
            {
                _logger.LogInformation("Metadata discovery service disabled");
                return;
            }

            _logger.LogInformation("Starting metadata discovery service");

            // Initial discovery
            await DiscoverMetadataAsync(stoppingToken);

            // Set up refresh timer if interval is specified
            if (_options.MetadataRefreshInterval > TimeSpan.Zero)
            {
                _refreshTimer = new Timer(
                    async _ => await DiscoverMetadataAsync(stoppingToken),
                    null,
                    _options.MetadataRefreshInterval,
                    _options.MetadataRefreshInterval);

                _logger.LogInformation("Metadata refresh timer set to {Interval}", _options.MetadataRefreshInterval);
            }

            // Keep the service running
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Metadata discovery service stopping");
            }
        }

        /// <summary>
        /// Disposes the service resources.
        /// </summary>
        public override void Dispose()
        {
            _refreshTimer?.Dispose();
            base.Dispose();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Fetches metadata from the configured endpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The metadata XML string.</returns>
        private async Task<string> FetchMetadataAsync(CancellationToken cancellationToken)
        {
            if (_httpClientFactory is null)
            {
                throw new InvalidOperationException("HTTP client factory is not available for metadata discovery");
            }

            var metadataUrl = GetMetadataUrl();
            _logger.LogDebug("Fetching metadata from {MetadataUrl}", metadataUrl);

            using var httpClient = _httpClientFactory.CreateClient("MetadataDiscovery");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/xml");

            var response = await httpClient.GetAsync(metadataUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var metadata = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Fetched {MetadataSize} characters of metadata", metadata.Length);

            return metadata;
        }

        /// <summary>
        /// Gets the metadata URL for discovery.
        /// </summary>
        /// <returns>The metadata URL.</returns>
        private string GetMetadataUrl()
        {
            if (!string.IsNullOrWhiteSpace(_options.ServiceRootUrl))
            {
                return $"{_options.ServiceRootUrl.TrimEnd('/')}{_options.MetadataPath}";
            }

            // For middleware, we'll need to construct this dynamically
            // This is a placeholder - in real implementation, this would be resolved from the current request context
            return $"http://localhost{_options.MetadataPath}";
        }

        /// <summary>
        /// Sets the model internally and raises events.
        /// </summary>
        /// <param name="model">The model to set.</param>
        /// <param name="source">The source of the model.</param>
        /// <param name="isUpdate">Whether this is an update to existing metadata.</param>
        private void SetModelInternal(EdmModel model, string source, bool isUpdate)
        {
            lock (_lock)
            {
                _currentModel = model;
                _lastUpdated = DateTime.UtcNow;
            }

            _logger.LogInformation("OData model updated from {Source} with {EntityTypeCount} entity types", 
                source, model.EntityTypes.Count);

            OnMetadataDiscovered(new MetadataDiscoveredEventArgs
            {
                Model = model,
                Source = source,
                IsUpdate = isUpdate,
                Context = new Dictionary<string, object>
                {
                    ["EntityTypeCount"] = model.EntityTypes.Count,
                    ["ComplexTypeCount"] = model.ComplexTypes.Count,
                    ["EntitySetCount"] = model.EntityContainer?.EntitySets.Count ?? 0
                }
            });
        }

        /// <summary>
        /// Raises the MetadataDiscovered event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        private void OnMetadataDiscovered(MetadataDiscoveredEventArgs args)
        {
            try
            {
                MetadataDiscovered?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MetadataDiscovered event handler");
            }
        }

        /// <summary>
        /// Raises the MetadataDiscoveryFailed event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        private void OnMetadataDiscoveryFailed(MetadataDiscoveryFailedEventArgs args)
        {
            try
            {
                MetadataDiscoveryFailed?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MetadataDiscoveryFailed event handler");
            }
        }

        #endregion
    }
}