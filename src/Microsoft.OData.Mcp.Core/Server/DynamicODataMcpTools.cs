// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Core.Server
{
    /// <summary>
    /// Dynamic OData MCP tools that generate methods based on discovered OData metadata.
    /// </summary>
    /// <remarks>
    /// This class provides dynamically generated MCP tools based on the structure of OData services.
    /// It discovers entity sets, operations, and schemas to create contextual tools for AI models.
    /// </remarks>
    [McpServerToolType]
    public class DynamicODataMcpTools
    {
        internal readonly IHttpClientFactory _httpClientFactory;
        internal readonly IOptions<McpServerConfiguration> _configuration;
        internal readonly ICsdlMetadataParser _metadataParser;
        internal readonly ILogger<DynamicODataMcpTools> _logger;
        internal readonly IReadOnlyList<EdmModel> _models;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicODataMcpTools"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="configuration">The server configuration.</param>
        /// <param name="metadataParser">The CSDL metadata parser.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="edmModels">The collection of EDM models to use for tool operations.</param>
        public DynamicODataMcpTools(
            IHttpClientFactory httpClientFactory,
            IOptions<McpServerConfiguration> configuration,
            ICsdlMetadataParser metadataParser,
            ILogger<DynamicODataMcpTools> logger,
            IEnumerable<EdmModel> edmModels)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _metadataParser = metadataParser ?? throw new ArgumentNullException(nameof(metadataParser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ArgumentNullException.ThrowIfNull(edmModels);

            _models = edmModels.ToList().AsReadOnly();

            if (_models.Count == 0)
            {
                throw new ArgumentException("At least one EDM model must be provided", nameof(edmModels));
            }

            _logger.LogDebug("Initialized DynamicODataMcpTools with {ModelCount} EDM model(s)", _models.Count);
        }

        /// <summary>
        /// Discovers and lists all available entity sets in the OData service.
        /// </summary>
        /// <returns>A JSON array of entity set information including names and types.</returns>
        [McpServerTool]
        [Description("Discovers and lists all available entity sets in the OData service")]
        public string DiscoverEntitySets()
        {
            try
            {
                var entitySets = new List<object>();

                // Aggregate entity sets from all registered models
                foreach (var model in _models)
                {
                    foreach (var container in model.EntityContainers)
                    {
                        foreach (var entitySet in container.EntitySets)
                        {
                            // Find the corresponding entity type
                            var entityType = model.EntityTypes.FirstOrDefault(et =>
                                et.FullName == entitySet.EntityType || et.Name == entitySet.EntityType);

                            var entitySetInfo = new
                            {
                                Name = entitySet.Name,
                                EntityType = entitySet.EntityType,
                                Container = container.Name,
                                Namespace = model.Namespaces.FirstOrDefault(),
                                Properties = entityType?.Properties.Select(p => new
                                {
                                    Name = p.Name,
                                    Type = p.Type,
                                    IsKey = p.IsKey,
                                    Nullable = p.Nullable
                                }).ToArray() ?? Array.Empty<object>(),
                                NavigationProperties = entityType?.NavigationProperties.Select(np => new
                                {
                                    Name = np.Name,
                                    Type = np.Type,
                                    IsCollection = np.Type?.Contains("Collection(") == true
                                }).ToArray() ?? Array.Empty<object>()
                            };

                            entitySets.Add(entitySetInfo);
                        }
                    }
                }

                return JsonSerializer.Serialize(entitySets, JsonConstants.PrettyPrint);
            }
            catch (Exception ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "Error discovering entity sets");
                throw new InvalidOperationException($"Failed to discover entity sets: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        /// <summary>
        /// Gets detailed schema information for a specific entity type.
        /// </summary>
        /// <param name="entityTypeName">The name of the entity type to describe.</param>
        /// <returns>Detailed schema information including properties, keys, and navigation properties.</returns>
        [McpServerTool]
        [Description("Gets detailed schema information for a specific entity type. Pass parameter directly as named parameter (e.g., entityTypeName='Region'). Do NOT wrap in 'parameters' object.")]
        public string DescribeEntityType(
            [Description("The name of the entity type to describe")] string entityTypeName)
        {
            try
            {
                // Search all models for the entity type
                EdmEntityType? entityType = null;
                foreach (var model in _models)
                {
                    entityType = model.EntityTypes.FirstOrDefault(et =>
                        et.Name.Equals(entityTypeName, StringComparison.OrdinalIgnoreCase) ||
                        et.FullName.Equals(entityTypeName, StringComparison.OrdinalIgnoreCase));

                    if (entityType is not null)
                    {
                        break;
                    }
                }

                if (entityType is null)
                {
                    throw new ArgumentException($"Entity type '{entityTypeName}' not found in any registered model");
                }

                var description = new
                {
                    Name = entityType.Name,
                    FullName = entityType.FullName,
                    Namespace = entityType.Namespace,
                    BaseType = entityType.BaseType,
                    IsAbstract = entityType.Abstract,
                    HasStream = entityType.HasStream,
                    KeyProperties = entityType.Key.ToArray(),
                    Properties = entityType.Properties.Select(p => new
                    {
                        Name = p.Name,
                        Type = p.Type,
                        IsKey = p.IsKey,
                        Nullable = p.Nullable,
                        MaxLength = p.MaxLength,
                        Precision = p.Precision,
                        Scale = p.Scale,
                        DefaultValue = p.DefaultValue,
                        Description = $"Property of type {p.Type}" + (p.IsKey ? " (Key)" : "")
                    }).ToArray(),
                    NavigationProperties = entityType.NavigationProperties.Select(np => new
                    {
                        Name = np.Name,
                        Type = np.Type,
                        IsCollection = np.Type?.Contains("Collection(") == true,
                        Partner = np.Partner,
                        ContainsTarget = np.ContainsTarget,
                        OnDelete = np.OnDelete,
                        ReferentialConstraints = np.ReferentialConstraints.Select(rc => new
                        {
                            Property = rc.Property,
                            ReferencedProperty = rc.ReferencedProperty
                        }).ToArray(),
                        Description = $"Navigation to {np.Type}"
                    }).ToArray()
                };

                return JsonSerializer.Serialize(description, JsonConstants.PrettyPrint);
            }
            catch (Exception ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "Error describing entity type: {EntityType}", entityTypeName);
                throw new InvalidOperationException($"Failed to describe entity type: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        /// <summary>
        /// Generates sample OData query URLs for a specific entity set.
        /// </summary>
        /// <param name="entitySetName">The name of the entity set.</param>
        /// <param name="includeAdvanced">Whether to include advanced query examples.</param>
        /// <returns>A collection of sample OData query URLs with explanations.</returns>
        [McpServerTool]
        [Description("Generates sample OData query URLs for a specific entity set. Pass parameters directly as named parameters (e.g., entitySetName='Countries', includeAdvanced=true). Do NOT wrap in 'parameters' object.")]
        public string GenerateQueryExamples(
            [Description("The name of the entity set")] string entitySetName,
            [Description("Whether to include advanced query examples")] bool includeAdvanced = false)
        {
            try
            {
                var config = _configuration.Value;

                // Search all models for the entity set and its type
                EdmEntitySet? targetEntitySet = null;
                EdmEntityType? entityType = null;

                foreach (var model in _models)
                {
                    foreach (var container in model.EntityContainers)
                    {
                        targetEntitySet = container.EntitySets.FirstOrDefault(es =>
                            es.Name.Equals(entitySetName, StringComparison.OrdinalIgnoreCase));
                        if (targetEntitySet is not null)
                        {
                            entityType = model.EntityTypes.FirstOrDefault(et =>
                                et.FullName == targetEntitySet.EntityType || et.Name == targetEntitySet.EntityType);
                            break;
                        }
                    }

                    if (targetEntitySet is not null)
                    {
                        break;
                    }
                }

                if (targetEntitySet is null)
                {
                    throw new ArgumentException($"Entity set '{entitySetName}' not found in any registered model");
                }

                var baseUrl = config.ODataService.BaseUrl?.TrimEnd('/') ?? throw new InvalidOperationException("OData service base URL is not configured");
                var examples = new List<object>
                {
                    // Basic queries
                    new
                    {
                        Description = "Get all entities",
                        Url = $"{baseUrl}/{entitySetName}",
                        ODataOptions = "None"
                    },
                    new
                    {
                        Description = "Get top 10 entities",
                        Url = $"{baseUrl}/{entitySetName}?$top=10",
                        ODataOptions = "$top"
                    },
                    new
                    {
                        Description = "Get entities with count",
                        Url = $"{baseUrl}/{entitySetName}?$count=true",
                        ODataOptions = "$count"
                    }
                };

                if (entityType != null)
                {
                    // Key-based access
                    var keyProperty = entityType.Properties.FirstOrDefault(p => p.IsKey);
                    if (keyProperty != null)
                    {
                        var sampleKey = GetSampleKeyValue(keyProperty.Type);
                        examples.Add(new
                        {
                            Description = $"Get entity by key ({keyProperty.Name})",
                            Url = $"{baseUrl}/{entitySetName}({sampleKey})",
                            ODataOptions = "Key access"
                        });
                    }

                    // Property selection
                    var firstFewProperties = entityType.Properties.Take(3).Select(p => p.Name);
                    if (firstFewProperties.Any())
                    {
                        examples.Add(new
                        {
                            Description = "Select specific properties",
                            Url = $"{baseUrl}/{entitySetName}?$select={string.Join(",", firstFewProperties)}",
                            ODataOptions = "$select"
                        });
                    }

                    // Filtering examples
                    var stringProperty = entityType.Properties.FirstOrDefault(p => 
                        p.Type?.Contains("String") == true);
                    if (stringProperty != null)
                    {
                        examples.Add(new
                        {
                            Description = $"Filter by string property ({stringProperty.Name})",
                            Url = $"{baseUrl}/{entitySetName}?$filter=startswith({stringProperty.Name},'A')",
                            ODataOptions = "$filter with string functions"
                        });
                    }

                    var numericProperty = entityType.Properties.FirstOrDefault(p => 
                        p.Type?.Contains("Int") == true || p.Type?.Contains("Decimal") == true);
                    if (numericProperty != null)
                    {
                        examples.Add(new
                        {
                            Description = $"Filter by numeric property ({numericProperty.Name})",
                            Url = $"{baseUrl}/{entitySetName}?$filter={numericProperty.Name} gt 0",
                            ODataOptions = "$filter with comparison"
                        });
                    }

                    // Navigation examples
                    var navProperty = entityType.NavigationProperties.FirstOrDefault();
                    if (navProperty != null)
                    {
                        examples.Add(new
                        {
                            Description = $"Expand navigation property ({navProperty.Name})",
                            Url = $"{baseUrl}/{entitySetName}?$expand={navProperty.Name}",
                            ODataOptions = "$expand"
                        });
                    }

                    // Advanced examples
                    if (includeAdvanced)
                    {
                        examples.Add(new
                        {
                            Description = "Complex query with multiple options",
                            Url = $"{baseUrl}/{entitySetName}?$top=5&$skip=10&$count=true&$orderby={entityType.Properties.First().Name}",
                            ODataOptions = "$top, $skip, $count, $orderby"
                        });

                        if (stringProperty != null && numericProperty != null)
                        {
                            examples.Add(new
                            {
                                Description = "Complex filter with multiple conditions",
                                Url = $"{baseUrl}/{entitySetName}?$filter={stringProperty.Name} ne null and {numericProperty.Name} gt 0",
                                ODataOptions = "$filter with logical operators"
                            });
                        }

                        if (navProperty != null)
                        {
                            examples.Add(new
                            {
                                Description = "Filter with navigation property",
                                Url = $"{baseUrl}/{entitySetName}?$filter={navProperty.Name}/any()",
                                ODataOptions = "$filter with lambda operators"
                            });
                        }
                    }
                }

                var result = new
                {
                    EntitySet = entitySetName,
                    BaseUrl = baseUrl,
                    TotalExamples = examples.Count,
                    Examples = examples
                };

                return JsonSerializer.Serialize(result, JsonConstants.PrettyPrint);
            }
            catch (Exception ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "Error generating query examples for: {EntitySet}", entitySetName);
                throw new InvalidOperationException($"Failed to generate query examples: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        /// <summary>
        /// Validates an OData query URL against the service metadata.
        /// </summary>
        /// <param name="queryUrl">The OData query URL to validate.</param>
        /// <returns>Validation results including any errors or warnings.</returns>
        [McpServerTool]
        [Description("Validates an OData query URL against the service metadata. Pass parameter directly as named parameter (e.g., queryUrl='https://...'). Do NOT wrap in 'parameters' object.")]
        public string ValidateQuery(
            [Description("The OData query URL to validate")] string queryUrl)
        {
            try
            {
                var config = _configuration.Value;
                var baseUrl = config.ODataService.BaseUrl?.TrimEnd('/') ?? throw new InvalidOperationException("OData service base URL is not configured");

                var validation = new
                {
                    QueryUrl = queryUrl,
                    IsValid = true,
                    Errors = new List<string>(),
                    Warnings = new List<string>(),
                    Suggestions = new List<string>()
                };

                // Basic URL validation
                if (!Uri.TryCreate(queryUrl, UriKind.Absolute, out var uri))
                {
                    validation.Errors.Add("Invalid URL format");
                    return JsonSerializer.Serialize(new { validation.QueryUrl, IsValid = false, validation.Errors },
                        JsonConstants.PrettyPrint);
                }

                // Check if URL starts with base URL
                if (!queryUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    validation.Warnings.Add($"URL does not start with configured base URL: {baseUrl}");
                }

                // Extract entity set name
                var path = uri.AbsolutePath.Replace(new Uri(baseUrl).AbsolutePath, "").Trim('/');
                var pathSegments = path.Split('/');

                if (pathSegments.Length > 0)
                {
                    var entitySetName = pathSegments[0].Split('(')[0]; // Remove key if present

                    // Check if entity set exists in any model
                    var entitySetExists = _models
                        .SelectMany(m => m.EntityContainers)
                        .SelectMany(c => c.EntitySets)
                        .Any(es => es.Name.Equals(entitySetName, StringComparison.OrdinalIgnoreCase));

                    if (!entitySetExists)
                    {
                        validation.Errors.Add($"Entity set '{entitySetName}' not found in any registered model");

                        // Suggest similar names from all models
                        var similarNames = _models
                            .SelectMany(m => m.EntityContainers)
                            .SelectMany(c => c.EntitySets)
                            .Where(es => es.Name.Contains(entitySetName, StringComparison.OrdinalIgnoreCase))
                            .Select(es => es.Name)
                            .Distinct()
                            .ToList();

                        if (similarNames.Any())
                        {
                            validation.Suggestions.Add($"Did you mean: {string.Join(", ", similarNames)}?");
                        }
                    }
                }

                // Validate query parameters
                var query = uri.Query;
                if (!string.IsNullOrEmpty(query))
                {
                    // Parse query string manually since System.Web.HttpUtility isn't available in .NET Core
                    var queryParams = ParseQueryString(query);
                    
                    foreach (string? key in queryParams.Keys)
                    {
                        if (key == null) continue;
                        
                        if (key.StartsWith("$"))
                        {
                            // Validate OData system query options
                            var validOptions = new[] { "$filter", "$orderby", "$select", "$expand", "$top", "$skip", "$count", "$search", "$format" };
                            if (!validOptions.Contains(key, StringComparer.OrdinalIgnoreCase))
                            {
                                validation.Warnings.Add($"Unknown OData query option: {key}");
                            }
                        }
                        else
                        {
                            validation.Warnings.Add($"Non-OData query parameter detected: {key}");
                        }
                    }
                }

                var finalValidation = new
                {
                    validation.QueryUrl,
                    IsValid = validation.Errors.Count == 0,
                    validation.Errors,
                    validation.Warnings,
                    validation.Suggestions
                };

                return JsonSerializer.Serialize(finalValidation, JsonConstants.PrettyPrint);
            }
            catch (Exception ex)
            {
                var betterEx = ex.Demystify();
                _logger.LogError(betterEx, "Error validating query: {QueryUrl}", queryUrl);
                throw new InvalidOperationException($"Failed to validate query: {betterEx.Message}\n{betterEx.StackTrace}", ex);
            }
        }

        #region Internal Methods

        /// <summary>
        /// Gets a sample key value based on the property type.
        /// </summary>
        internal static string GetSampleKeyValue(string? propertyType)
        {
            return propertyType?.ToLowerInvariant() switch
            {
                var t when t?.Contains("int") == true => "1",
                var t when t?.Contains("guid") == true => "guid'00000000-0000-0000-0000-000000000001'",
                var t when t?.Contains("string") == true => "'sample'",
                var t when t?.Contains("decimal") == true => "1.0",
                var t when t?.Contains("double") == true => "1.0",
                var t when t?.Contains("datetime") == true => "datetime'2023-01-01T00:00:00'",
                _ => "'sample'"
            };
        }

        /// <summary>
        /// Parses a query string into a dictionary.
        /// </summary>
        internal static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>();
            
            if (string.IsNullOrEmpty(query))
                return result;
            
            // Remove leading '?' if present
            if (query.StartsWith("?"))
                query = query.Substring(1);
            
            var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(keyValue[0]);
                var value = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]) : string.Empty;
                
                result[key] = value;
            }
            
            return result;
        }

        #endregion
    }
}
