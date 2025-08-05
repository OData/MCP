using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Timer = System.Timers.Timer;

namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Hosted service that periodically discovers and refreshes OData metadata.
    /// </summary>
    /// <remarks>
    /// This service automatically discovers OData metadata from configured services and
    /// refreshes it at regular intervals to ensure the MCP tools stay current.
    /// </remarks>
    public sealed class MetadataDiscoveryService : IHostedService, IDisposable
    {
        #region Fields

        private readonly IOptions<McpServerConfiguration> _configuration;
        private readonly CsdlParser _metadataParser;
        private readonly ILogger<MetadataDiscoveryService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Timer? _refreshTimer;
        private readonly SemaphoreSlim _refreshSemaphore;
        private bool _disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataDiscoveryService"/> class.
        /// </summary>
        /// <param name="configuration">The MCP server configuration.</param>
        /// <param name="metadataParser">The CSDL metadata parser.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="serviceProvider">The service provider for dependency resolution.</param>
        public MetadataDiscoveryService(
            IOptions<McpServerConfiguration> configuration,
            CsdlParser metadataParser,
            ILogger<MetadataDiscoveryService> logger,
            IServiceProvider serviceProvider)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _metadataParser = metadataParser ?? throw new ArgumentNullException(nameof(metadataParser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _refreshSemaphore = new SemaphoreSlim(1, 1);
        }

        #endregion

        #region IHostedService Implementation

        /// <summary>
        /// Starts the metadata discovery service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting metadata discovery service");

            var config = _configuration.Value;

            if (!config.ODataService.AutoDiscoverMetadata)
            {
                _logger.LogInformation("Metadata auto-discovery is disabled");
                return;
            }

            try
            {
                // Perform initial metadata discovery
                await DiscoverMetadataAsync(cancellationToken);

                // Set up periodic refresh if enabled
                if (config.ODataService.RefreshInterval > TimeSpan.Zero)
                {
                    SetupPeriodicRefresh(config.ODataService.RefreshInterval);
                }

                _logger.LogInformation("Metadata discovery service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start metadata discovery service");
                throw;
            }
        }

        /// <summary>
        /// Stops the metadata discovery service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping metadata discovery service");

            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshTimer = null;

            _logger.LogInformation("Metadata discovery service stopped");
            return Task.CompletedTask;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually triggers metadata discovery.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RefreshMetadataAsync(CancellationToken cancellationToken = default)
        {
            await DiscoverMetadataAsync(cancellationToken);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Sets up the periodic metadata refresh timer.
        /// </summary>
        /// <param name="interval">The refresh interval.</param>
        private void SetupPeriodicRefresh(TimeSpan interval)
        {
            _logger.LogInformation("Setting up periodic metadata refresh every {Interval}", interval);

            _refreshTimer = new Timer(interval.TotalMilliseconds);
            _refreshTimer.Elapsed += OnRefreshTimerElapsed;
            _refreshTimer.AutoReset = true;
            _refreshTimer.Enabled = true;
        }

        /// <summary>
        /// Handles the refresh timer elapsed event.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnRefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await DiscoverMetadataAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during periodic metadata refresh");
                }
            });
        }

        /// <summary>
        /// Discovers and processes OData metadata.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task DiscoverMetadataAsync(CancellationToken cancellationToken)
        {
            if (!await _refreshSemaphore.WaitAsync(TimeSpan.FromMinutes(1), cancellationToken))
            {
                _logger.LogWarning("Metadata discovery is already in progress, skipping this refresh");
                return;
            }

            try
            {
                _logger.LogDebug("Starting metadata discovery");

                var config = _configuration.Value;
                
                if (string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
                {
                    _logger.LogWarning("OData service URL is not configured, skipping metadata discovery");
                    return;
                }

                var metadataUrl = BuildMetadataUrl(config.ODataService);
                _logger.LogDebug("Fetching metadata from: {MetadataUrl}", metadataUrl);

                var httpClient = CreateHttpClient(config.ODataService);
                
                using var response = await httpClient.GetAsync(metadataUrl, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch metadata: {StatusCode} {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    return;
                }

                var metadataXml = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (string.IsNullOrWhiteSpace(metadataXml))
                {
                    _logger.LogWarning("Received empty metadata response");
                    return;
                }

                _logger.LogDebug("Parsing metadata XML ({Length} characters)", metadataXml.Length);

                // Parse the metadata
                var entityModel = _metadataParser.ParseFromString(metadataXml);

                _logger.LogInformation("Successfully discovered metadata: {EntitySetCount} entity sets, {EntityTypeCount} entity types",
                    entityModel.AllEntitySets.Count(), entityModel.EntityTypes.Count);

                // TODO: Cache the metadata and regenerate MCP tools
                // This would integrate with the caching system and tool factory

                LogMetadataStatistics(entityModel);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while fetching metadata");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError("Timeout while fetching metadata");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during metadata discovery");
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        /// <summary>
        /// Builds the metadata URL from the service configuration.
        /// </summary>
        /// <param name="serviceConfig">The OData service configuration.</param>
        /// <returns>The complete metadata URL.</returns>
        private static string BuildMetadataUrl(ODataServiceConfiguration serviceConfig)
        {
            var baseUrl = serviceConfig.BaseUrl!.TrimEnd('/');
            var metadataPath = serviceConfig.MetadataPath.TrimStart('/');
            return $"{baseUrl}/{metadataPath}";
        }

        /// <summary>
        /// Creates an HTTP client configured for the OData service.
        /// </summary>
        /// <param name="serviceConfig">The OData service configuration.</param>
        /// <returns>A configured HTTP client.</returns>
        private HttpClient CreateHttpClient(ODataServiceConfiguration serviceConfig)
        {
            var httpClient = new HttpClient();
            
            // Set timeout
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            // Add user agent
            httpClient.DefaultRequestHeaders.Add("User-Agent", "OData-MCP-Server/1.0");

            // Add accept header
            httpClient.DefaultRequestHeaders.Add("Accept", "application/xml, text/xml");

            // Configure authentication if required
            ConfigureAuthentication(httpClient, serviceConfig.Authentication);

            return httpClient;
        }

        /// <summary>
        /// Configures authentication for the HTTP client.
        /// </summary>
        /// <param name="httpClient">The HTTP client to configure.</param>
        /// <param name="authConfig">The authentication configuration.</param>
        private static void ConfigureAuthentication(HttpClient httpClient, ODataAuthenticationConfiguration authConfig)
        {
            switch (authConfig.Type)
            {
                case ODataAuthenticationType.None:
                    // No authentication required
                    break;

                case ODataAuthenticationType.ApiKey:
                    if (!string.IsNullOrWhiteSpace(authConfig.ApiKey) && 
                        !string.IsNullOrWhiteSpace(authConfig.ApiKeyHeader))
                    {
                        httpClient.DefaultRequestHeaders.Add(authConfig.ApiKeyHeader, authConfig.ApiKey);
                    }
                    break;

                case ODataAuthenticationType.Bearer:
                    if (!string.IsNullOrWhiteSpace(authConfig.BearerToken))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authConfig.BearerToken);
                    }
                    break;

                case ODataAuthenticationType.Basic:
                    if (authConfig.BasicAuth is not null &&
                        !string.IsNullOrWhiteSpace(authConfig.BasicAuth.Username) && 
                        !string.IsNullOrWhiteSpace(authConfig.BasicAuth.Password))
                    {
                        var credentials = Convert.ToBase64String(
                            System.Text.Encoding.UTF8.GetBytes($"{authConfig.BasicAuth.Username}:{authConfig.BasicAuth.Password}"));
                        httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                    }
                    break;

                case ODataAuthenticationType.OAuth2:
                    // OAuth2 would require more complex token acquisition logic
                    // This is a placeholder for future implementation
                    break;
            }
        }

        /// <summary>
        /// Logs statistics about the discovered metadata.
        /// </summary>
        /// <param name="entityModel">The entity model to analyze.</param>
        private void LogMetadataStatistics(EdmModel entityModel)
        {
            _logger.LogDebug("Metadata Statistics:");
            _logger.LogDebug("  Entity Sets: {Count}", entityModel.AllEntitySets.Count());
            _logger.LogDebug("  Entity Types: {Count}", entityModel.EntityTypes.Count);
            _logger.LogDebug("  Complex Types: {Count}", entityModel.ComplexTypes.Count);
            _logger.LogDebug("  Entity Containers: {Count}", entityModel.EntityContainers.Count);

            // Log entity sets with their types
            var entitySetsArray = entityModel.AllEntitySets.Take(10).ToArray(); // Limit to first 10 for readability
            foreach (var entitySet in entitySetsArray)
            {
                _logger.LogDebug("  Entity Set: {Name} -> {Type}", entitySet.Name, entitySet.EntityType);
            }

            var totalEntitySets = entityModel.AllEntitySets.Count();
            if (totalEntitySets > 10)
            {
                _logger.LogDebug("  ... and {More} more entity sets", totalEntitySets - 10);
            }

            // Log navigation properties count
            var totalNavigationProperties = entityModel.EntityTypes
                .SelectMany(et => et.NavigationProperties)
                .Count();
            _logger.LogDebug("  Total Navigation Properties: {Count}", totalNavigationProperties);

            // Log property statistics
            var totalProperties = entityModel.EntityTypes
                .SelectMany(et => et.Properties)
                .Count();
            _logger.LogDebug("  Total Properties: {Count}", totalProperties);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the metadata discovery service.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _refreshTimer?.Dispose();
            _refreshSemaphore?.Dispose();
            _disposed = true;
        }

        #endregion
    }
}