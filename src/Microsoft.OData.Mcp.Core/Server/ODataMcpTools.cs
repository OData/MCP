// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Constants;
using ModelContextProtocol.Server;

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
        internal readonly IHttpClientFactory _httpClientFactory;
        internal readonly IOptions<McpServerConfiguration> _configuration;
        internal readonly ILogger<ODataMcpTools> _logger;

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
        [Description("Queries an OData entity set with optional filtering, sorting, and pagination. IMPORTANT: Pass parameters directly as named parameters (e.g., entitySet='Countries', top=10, count=true). Do NOT use $ prefixes in parameter names - they are added automatically. Do NOT wrap parameters in a 'parameters' object.")]
        public async Task<string> QueryEntitySet(
            [Description("The name of the entity set to query")] string entitySet,
            [Description("OData filter expression WITHOUT $ prefix (e.g., \"Name eq 'John'\" becomes $filter=Name eq 'John')")] string? filter = null,
            [Description("OData orderby expression WITHOUT $ prefix (e.g., 'Name desc' becomes $orderby=Name desc)")] string? orderby = null,
            [Description("Comma-separated list of properties to select WITHOUT $ prefix")] string? select = null,
            [Description("Maximum number of results to return (becomes $top parameter)")] int? top = null,
            [Description("Number of results to skip for pagination (becomes $skip parameter)")] int? skip = null,
            [Description("Include the total count of matching entities (becomes $count parameter)")] bool count = false)
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
                {
                    // UriBuilder.Query adds '?' automatically, so just use the joined params
                    queryBuilder.Query = string.Join("&", queryParams);
                }

                var finalUrl = queryBuilder.Uri.ToString();
                _logger.LogDebug("Executing OData query: {QueryUrl}", finalUrl);
                _logger.LogDebug("Query string breakdown: {QueryParams}", string.Join(", ", queryParams));

                // Execute the query
                using var httpClient = _httpClientFactory.CreateClient("OData");
                var response = await httpClient.GetAsync(finalUrl);
                
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                // Parse and format the response
                var json = JsonDocument.Parse(content);
                return JsonSerializer.Serialize(json, JsonConstants.PrettyPrint);
            }
            catch (HttpRequestException ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "HTTP error executing OData query");
                throw new InvalidOperationException($"Failed to execute OData query: {betterEx.Message}\n{betterEx.StackTrace}", ex);
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
        [Description("Gets a single entity by its key from an OData service. Pass parameters directly as named parameters (e.g., entitySet='Countries', key='123'). Do NOT wrap in 'parameters' object.")]
        public async Task<string> GetEntity(
            [Description("The name of the entity set")] string entitySet,
            [Description("The entity key value")] string key,
            [Description("Comma-separated list of properties to select WITHOUT $ prefix")] string? select = null)
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
                return JsonSerializer.Serialize(json, JsonConstants.PrettyPrint);
            }
            catch (HttpRequestException ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "HTTP error getting OData entity");
                throw new InvalidOperationException($"Failed to get OData entity: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        /// <summary>
        /// Creates a new entity in the specified OData entity set.
        /// </summary>
        /// <param name="entitySet">The name of the entity set.</param>
        /// <param name="entity">The entity data as JSON.</param>
        /// <returns>The created entity as JSON.</returns>
        [McpServerTool]
        [Description("Creates a new entity in the specified OData entity set. Pass parameters directly as named parameters (e.g., entitySet='Countries', entity='{...}'). Do NOT wrap in 'parameters' object.")]
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
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "Invalid JSON provided for entity creation");
                throw new ArgumentException($"Invalid JSON format: {betterEx.Message}\n{betterEx.StackTrace}", nameof(entity), ex);
            }
            catch (HttpRequestException ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "HTTP error creating entity");
                throw new InvalidOperationException($"Failed to create entity: {betterEx.Message}\n{betterEx.StackTrace}", ex);
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

                // Use byte array read for better encoding handling
                var bytes = await response.Content.ReadAsByteArrayAsync();
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch (HttpRequestException ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "HTTP error getting OData metadata");
                throw new InvalidOperationException($"Failed to get OData metadata: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        /// <summary>
        /// Updates an existing entity in the OData service.
        /// </summary>
        /// <param name="entitySet">The entity set name.</param>
        /// <param name="key">The entity key.</param>
        /// <param name="entity">The updated entity data as JSON.</param>
        /// <returns>The updated entity.</returns>
        [McpServerTool]
        [Description("Updates an existing entity in the OData service. Pass parameters directly as named parameters (e.g., entitySet='Countries', key='123', entity='{...}'). Do NOT wrap in 'parameters' object.")]
        public async Task<string> UpdateEntity(string entitySet, string key, string entity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entitySet);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(entity);

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
                var formattedKey = FormatKeyValue(key);
                var url = $"{config.ODataService.BaseUrl.TrimEnd('/')}/{entitySet}({formattedKey})";
                _logger.LogDebug("Updating entity in {EntitySet} with key {Key}: {Url}", entitySet, key, url);

                // Create the request
                using var httpClient = _httpClientFactory.CreateClient("OData");
                using var content = new StringContent(entity, Encoding.UTF8, "application/json");
                
                // Use PATCH for partial updates
                using var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = content
                };
                request.Headers.Add("If-Match", "*"); // Override optimistic concurrency
                
                var response = await httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Failed to update entity: {response.StatusCode} - {error}");
                }
                
                // For successful updates, return the updated entity by fetching it
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    // Fetch the updated entity
                    return await GetEntity(entitySet, key);
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
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "Invalid JSON provided for entity update");
                throw new ArgumentException($"Invalid JSON format: {betterEx.Message}\n{betterEx.StackTrace}", nameof(entity), ex);
            }
            catch (HttpRequestException ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "HTTP error updating entity");
                throw new InvalidOperationException($"Failed to update entity: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        /// <summary>
        /// Deletes an entity from the OData service.
        /// </summary>
        /// <param name="entitySet">The entity set name.</param>
        /// <param name="key">The entity key.</param>
        /// <returns>Success message.</returns>
        [McpServerTool]
        [Description("Deletes an entity from the OData service. Pass parameters directly as named parameters (e.g., entitySet='Countries', key='123'). Do NOT wrap in 'parameters' object.")]
        public async Task<string> DeleteEntity(string entitySet, string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entitySet);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var config = _configuration.Value;
            
            if (string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
            {
                throw new InvalidOperationException("OData service URL is not configured");
            }

            try
            {
                // Build the URL
                var formattedKey = FormatKeyValue(key);
                var url = $"{config.ODataService.BaseUrl.TrimEnd('/')}/{entitySet}({formattedKey})";
                _logger.LogDebug("Deleting entity from {EntitySet} with key {Key}: {Url}", entitySet, key, url);

                // Create the request
                using var httpClient = _httpClientFactory.CreateClient("OData");
                
                var response = await httpClient.DeleteAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Failed to delete entity: {response.StatusCode} - {error}");
                }
                
                return JsonSerializer.Serialize(new 
                { 
                    success = true,
                    message = $"Entity with key '{key}' deleted successfully from {entitySet}"
                }, JsonConstants.PrettyPrint);
            }
            catch (HttpRequestException ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "HTTP error deleting entity");
                throw new InvalidOperationException($"Failed to delete entity: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        /// <summary>
        /// Navigates from a source entity to related entities via a navigation property.
        /// </summary>
        /// <param name="entitySet">The source entity set name.</param>
        /// <param name="key">The source entity key.</param>
        /// <param name="navigationProperty">The navigation property name.</param>
        /// <returns>The related entities.</returns>
        [McpServerTool]
        [Description("Navigates from a source entity to related entities via a navigation property. Pass parameters directly as named parameters (e.g., entitySet='Countries', key='123', navigationProperty='Regions'). Do NOT wrap in 'parameters' object.")]
        public async Task<string> NavigateRelationship(string entitySet, string key, string navigationProperty)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entitySet);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(navigationProperty);

            var config = _configuration.Value;
            
            if (string.IsNullOrWhiteSpace(config.ODataService.BaseUrl))
            {
                throw new InvalidOperationException("OData service URL is not configured");
            }

            try
            {
                // Build the URL
                var formattedKey = FormatKeyValue(key);
                var url = $"{config.ODataService.BaseUrl.TrimEnd('/')}/{entitySet}({formattedKey})/{navigationProperty}";
                _logger.LogDebug("Navigating from {EntitySet}({Key}) via {NavigationProperty}: {Url}", 
                    entitySet, key, navigationProperty, url);

                // Create the request
                using var httpClient = _httpClientFactory.CreateClient("OData");
                
                var response = await httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Failed to navigate relationship: {response.StatusCode} - {error}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse and format the response
                var json = JsonDocument.Parse(responseContent);
                return JsonSerializer.Serialize(json, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch (HttpRequestException ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "HTTP error navigating relationship");
                throw new InvalidOperationException($"Failed to navigate relationship: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        #region Internal Methods

        /// <summary>
        /// Formats a key value for OData URLs.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <returns>The formatted key value.</returns>
        internal static string FormatKeyValue(string key)
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
