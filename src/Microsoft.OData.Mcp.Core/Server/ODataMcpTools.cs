using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Microsoft.OData.Mcp.Core.Configuration;

namespace Microsoft.OData.Mcp.Core.Server
{
    /// <summary>
    /// OData MCP tools using the official SDK attribute-based approach.
    /// </summary>
    /// <remarks>
    /// This class provides MCP tools for interacting with OData services using the official
    /// Model Context Protocol C# SDK patterns with attributes.
    /// </remarks>
    [McpServerToolType]
    public class ODataMcpTools
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<McpServerConfiguration> _configuration;
        private readonly ILogger<ODataMcpTools> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpTools"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="configuration">The server configuration.</param>
        /// <param name="logger">The logger instance.</param>
        public ODataMcpTools(
            IHttpClientFactory httpClientFactory,
            IOptions<McpServerConfiguration> configuration,
            ILogger<ODataMcpTools> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Queries an OData entity set with optional filtering, sorting, and pagination.
        /// </summary>
        /// <param name="entitySet">The name of the entity set to query.</param>
        /// <param name="filter">Optional OData filter expression.</param>
        /// <param name="orderby">Optional OData orderby expression.</param>
        /// <param name="select">Optional comma-separated list of properties to select.</param>
        /// <param name="top">Optional maximum number of results to return.</param>
        /// <param name="skip">Optional number of results to skip for pagination.</param>
        /// <param name="count">Optional flag to include the total count of matching entities.</param>
        /// <returns>The query results as JSON.</returns>
        [McpServerTool]
        [Description("Queries an OData entity set with optional filtering, sorting, and pagination")]
        public async Task<string> QueryEntitySet(
            [Description("The name of the entity set to query")] string entitySet,
            [Description("OData filter expression (e.g., 'Name eq \"John\"')")] string? filter = null,
            [Description("OData orderby expression (e.g., 'Name desc')")] string? orderby = null,
            [Description("Comma-separated list of properties to select")] string? select = null,
            [Description("Maximum number of results to return")] int? top = null,
            [Description("Number of results to skip for pagination")] int? skip = null,
            [Description("Include the total count of matching entities")] bool count = false)
        {
            var config = _configuration.Value;
            
            if (string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
            {
                throw new InvalidOperationException("OData service URL is not configured");
            }

            try
            {
                // Build the OData query URL
                var queryBuilder = new UriBuilder($"{config.ODataService.BaseUrl.TrimEnd('/')}/{entitySet}");
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(filter))
                    queryParams.Add($"$filter={Uri.EscapeDataString(filter)}");
                
                if (!string.IsNullOrWhiteSpace(orderby))
                    queryParams.Add($"$orderby={Uri.EscapeDataString(orderby)}");
                
                if (!string.IsNullOrWhiteSpace(select))
                    queryParams.Add($"$select={Uri.EscapeDataString(select)}");
                
                if (top.HasValue)
                    queryParams.Add($"$top={top.Value}");
                
                if (skip.HasValue)
                    queryParams.Add($"$skip={skip.Value}");
                
                if (count)
                    queryParams.Add("$count=true");

                if (queryParams.Count > 0)
                    queryBuilder.Query = string.Join("&", queryParams);

                _logger.LogDebug("Executing OData query: {QueryUrl}", queryBuilder.Uri);

                // Execute the query
                using var httpClient = _httpClientFactory.CreateClient("OData");
                var response = await httpClient.GetAsync(queryBuilder.Uri);
                
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                // Parse and format the response
                var json = JsonDocument.Parse(content);
                return JsonSerializer.Serialize(json, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error executing OData query");
                throw new InvalidOperationException($"Failed to execute OData query: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets a single entity by its key from an OData service.
        /// </summary>
        /// <param name="entitySet">The name of the entity set.</param>
        /// <param name="key">The entity key value as a string or number.</param>
        /// <param name="select">Optional comma-separated list of properties to select.</param>
        /// <returns>The entity data as JSON.</returns>
        [McpServerTool]
        [Description("Gets a single entity by its key from an OData service")]
        public async Task<string> GetEntity(
            [Description("The name of the entity set")] string entitySet,
            [Description("The entity key value")] string key,
            [Description("Comma-separated list of properties to select")] string? select = null)
        {
            var config = _configuration.Value;
            
            if (string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
            {
                throw new InvalidOperationException("OData service URL is not configured");
            }

            try
            {
                // Format key value
                var formattedKey = FormatKeyValue(key);
                
                // Build the entity URL
                var queryBuilder = new UriBuilder($"{config.ODataService.BaseUrl.TrimEnd('/')}/{entitySet}({formattedKey})");
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(select))
                    queryParams.Add($"$select={Uri.EscapeDataString(select)}");

                if (queryParams.Count > 0)
                    queryBuilder.Query = string.Join("&", queryParams);

                _logger.LogDebug("Getting OData entity: {QueryUrl}", queryBuilder.Uri);

                // Execute the request
                using var httpClient = _httpClientFactory.CreateClient("OData");
                var response = await httpClient.GetAsync(queryBuilder.Uri);
                
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                // Parse and format the response
                var json = JsonDocument.Parse(content);
                return JsonSerializer.Serialize(json, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error getting OData entity");
                throw new InvalidOperationException($"Failed to get OData entity: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a new entity in the specified OData entity set.
        /// </summary>
        /// <param name="entitySet">The name of the entity set.</param>
        /// <param name="entity">The entity data as JSON.</param>
        /// <returns>The created entity as JSON.</returns>
        [McpServerTool]
        [Description("Creates a new entity in the specified OData entity set")]
        public async Task<string> CreateEntity(
            [Description("The name of the entity set")] string entitySet,
            [Description("The entity data as JSON")] string entity)
        {
            var config = _configuration.Value;
            
            if (string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
            {
                throw new InvalidOperationException("OData service URL is not configured");
            }

            try
            {
                // Validate JSON
                using var jsonDoc = JsonDocument.Parse(entity);
                
                // Build the URL
                var url = $"{config.ODataService.BaseUrl.TrimEnd('/')}/{entitySet}";
                _logger.LogDebug("Creating entity in {EntitySet}: {Url}", entitySet, url);

                // Create the request
                using var httpClient = _httpClientFactory.CreateClient("OData");
                using var content = new StringContent(entity, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync(url, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Failed to create entity: {response.StatusCode} - {error}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse and format the response
                var json = JsonDocument.Parse(responseContent);
                return JsonSerializer.Serialize(json, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON provided for entity creation");
                throw new ArgumentException("Invalid JSON format", nameof(entity), ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating entity");
                throw new InvalidOperationException($"Failed to create entity: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the OData service metadata document.
        /// </summary>
        /// <returns>The OData metadata as XML.</returns>
        [McpServerTool]
        [Description("Gets the OData service metadata document")]
        public async Task<string> GetMetadata()
        {
            var config = _configuration.Value;
            
            if (string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
            {
                throw new InvalidOperationException("OData service URL is not configured");
            }

            try
            {
                var metadataUrl = $"{config.ODataService.BaseUrl.TrimEnd('/')}{config.ODataService.MetadataPath}";
                _logger.LogDebug("Getting OData metadata: {MetadataUrl}", metadataUrl);

                using var httpClient = _httpClientFactory.CreateClient("OData");
                var response = await httpClient.GetAsync(metadataUrl);
                
                response.EnsureSuccessStatusCode();
                
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error getting OData metadata");
                throw new InvalidOperationException($"Failed to get OData metadata: {ex.Message}", ex);
            }
        }

        #region Private Methods

        /// <summary>
        /// Formats a key value for OData URLs.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <returns>The formatted key value.</returns>
        private static string FormatKeyValue(string key)
        {
            // Check if it's a number
            if (int.TryParse(key, out _) || long.TryParse(key, out _) || 
                decimal.TryParse(key, out _) || double.TryParse(key, out _))
            {
                return key;
            }
            
            // Check if it's a GUID
            if (Guid.TryParse(key, out var guid))
            {
                return guid.ToString();
            }
            
            // Treat as string - wrap in quotes
            return $"'{Uri.EscapeDataString(key)}'";
        }

        #endregion
    }
}