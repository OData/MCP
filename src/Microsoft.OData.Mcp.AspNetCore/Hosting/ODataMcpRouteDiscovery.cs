// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Edm;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Discovers <c>(prefix, IEdmModel)</c> pairs from ASP.NET Core endpoint routing without compiling against
    /// Microsoft.AspNetCore.OData 7 or 8.
    /// </summary>
    public static class ODataMcpRouteDiscovery
    {

        #region Fields

        internal const string ODataAssemblyName = "Microsoft.AspNetCore.OData";

        internal const string ODataCatchAllMarker = "ODataEndpointPath_";

        internal const string ODataEightOptionsTypeName = "Microsoft.AspNetCore.OData.ODataOptions";

        internal const string ODataSevenOptionsTypeName = "Microsoft.AspNet.OData.ODataOptions";

        internal const string PerRouteContainerTypeName = "Microsoft.AspNet.OData.IPerRouteContainer";

        #endregion

        #region Public Methods

        /// <summary>
        /// Discovers OData prefixes and models from explicit registration, endpoint metadata, OData 7/Restier
        /// catch-all routes, and (when present) an already-loaded OData options dictionary.
        /// </summary>
        /// <param name="services">The application service provider.</param>
        /// <param name="options">Host include/exclude and explicit routes.</param>
        /// <returns>
        /// Bindings after include/exclude filters. Duplicate prefixes keep the last discovered model.
        /// </returns>
        public static IReadOnlyList<ODataMcpRouteBinding> Discover(IServiceProvider services, ODataMcpHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            var routes = new Dictionary<string, IEdmModel>(StringComparer.OrdinalIgnoreCase);
            TryAddFromExplicit(options, routes);
            TryAddFromEndpointDataSources(services, routes);
            TryAddFromRouteComponents(services, routes);

            return FilterRoutes(routes, options);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Applies <see cref="ODataMcpHostOptions.IncludePrefixes"/> then <see cref="ODataMcpHostOptions.ExcludeRoutes"/>.
        /// </summary>
        /// <param name="routes">Discovered prefix/model pairs.</param>
        /// <param name="options">Host options.</param>
        /// <returns>
        /// The filtered bindings.
        /// </returns>
        internal static IReadOnlyList<ODataMcpRouteBinding> FilterRoutes(IReadOnlyDictionary<string, IEdmModel> routes, ODataMcpHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(routes);
            ArgumentNullException.ThrowIfNull(options);

            var include = options.IncludePrefixes
                .Select(NormalizePrefix)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var exclude = options.ExcludeRoutes
                .Select(NormalizePrefix)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return [.. routes
                .Where(pair =>
                {
                    var prefix = NormalizePrefix(pair.Key);
                    if (include.Count > 0 && !include.Contains(prefix))
                    {
                        return false;
                    }

                    return !exclude.Contains(prefix);
                })
                .Select(pair => new ODataMcpRouteBinding
                {
                    Model = pair.Value,
                    Prefix = pair.Key
                })];
        }

        /// <summary>
        /// Normalizes a route prefix for include/exclude comparison.
        /// </summary>
        /// <param name="prefix">The prefix.</param>
        /// <returns>
        /// The prefix without leading or trailing slashes.
        /// </returns>
        internal static string NormalizePrefix(string? prefix)
        {
            return (prefix ?? string.Empty).Trim('/');
        }

        /// <summary>
        /// Parses an OData 7 / Restier catch-all template such as <c>odata/{**ODataEndpointPath_odata}</c>.
        /// </summary>
        /// <param name="rawText">The route pattern raw text.</param>
        /// <param name="prefix">The OData prefix, empty when the catch-all is at the root.</param>
        /// <param name="routeName">The OData route name after <c>ODataEndpointPath_</c>.</param>
        /// <returns>
        /// <c>true</c> when the template is an OData catch-all.
        /// </returns>
        internal static bool TryParseODataCatchAll(string? rawText, out string prefix, out string routeName)
        {
            prefix = string.Empty;
            routeName = string.Empty;
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return false;
            }

            var text = rawText.Trim().TrimStart('/');
            var markerIndex = text.IndexOf(ODataCatchAllMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            var brace = text.LastIndexOf('{', markerIndex);
            if (brace < 0)
            {
                return false;
            }

            var close = text.IndexOf('}', markerIndex);
            if (close < 0)
            {
                return false;
            }

            routeName = text[(markerIndex + ODataCatchAllMarker.Length)..close];
            if (string.IsNullOrWhiteSpace(routeName))
            {
                return false;
            }

            prefix = brace == 0
                ? string.Empty
                : text[..brace].TrimEnd('/');

            return true;
        }

        /// <summary>
        /// Reads <see cref="IEdmModel"/> from a duck-typed per-route container for <paramref name="routeName"/>.
        /// </summary>
        /// <param name="services">The application services.</param>
        /// <param name="routeName">The OData route name.</param>
        /// <returns>
        /// The model, or <c>null</c>.
        /// </returns>
        internal static IEdmModel? TryGetModelFromPerRouteContainer(IServiceProvider services, string routeName)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(routeName);

            var assembly = TryGetLoadedODataAssembly();
            var containerType = assembly?.GetType(PerRouteContainerTypeName);
            if (containerType is null)
            {
                return null;
            }

            var container = services.GetService(containerType);
            if (container is null)
            {
                return null;
            }

            var hasMethod = containerType.GetMethod("HasODataRootContainer", [typeof(string)]);
            if (hasMethod is not null)
            {
                var has = hasMethod.Invoke(container, [routeName]);
                if (has is false)
                {
                    return null;
                }
            }

            var getRoot = containerType.GetMethod("GetODataRootContainer", [typeof(string)]);
            if (getRoot is null)
            {
                return null;
            }

            try
            {
                if (getRoot.Invoke(container, [routeName]) is not IServiceProvider root)
                {
                    return null;
                }

                return root.GetService(typeof(IEdmModel)) as IEdmModel;
            }
            catch (TargetInvocationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the loaded Microsoft.AspNetCore.OData assembly, or <c>null</c> when the app did not load one.
        /// </summary>
        /// <returns>
        /// The assembly, or <c>null</c>.
        /// </returns>
        internal static Assembly? TryGetLoadedODataAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, ODataAssemblyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Copies explicit host registrations into <paramref name="routes"/>.
        /// </summary>
        /// <param name="options">Host options.</param>
        /// <param name="routes">The accumulating prefix map.</param>
        internal static void TryAddFromExplicit(ODataMcpHostOptions options, Dictionary<string, IEdmModel> routes)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(routes);

            foreach (var binding in options.ExplicitRoutes)
            {
                ArgumentNullException.ThrowIfNull(binding);
                ArgumentNullException.ThrowIfNull(binding.Prefix);
                ArgumentNullException.ThrowIfNull(binding.Model);
                routes[binding.Prefix] = binding.Model;
            }
        }

        /// <summary>
        /// Walks every <see cref="EndpointDataSource"/> for OData 8 metadata and OData 7/Restier catch-alls.
        /// </summary>
        /// <param name="services">The application services.</param>
        /// <param name="routes">The accumulating prefix map.</param>
        internal static void TryAddFromEndpointDataSources(IServiceProvider services, Dictionary<string, IEdmModel> routes)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(routes);

            foreach (var source in services.GetServices<EndpointDataSource>())
            {
                foreach (var endpoint in source.Endpoints)
                {
                    TryAddFromMetadata(endpoint, routes);
                    TryAddFromCatchAll(endpoint, services, routes);
                }
            }
        }

        /// <summary>
        /// Reads a duck-typed metadata object with string <c>Prefix</c> and <see cref="IEdmModel"/> <c>Model</c>.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="routes">The accumulating prefix map.</param>
        internal static void TryAddFromMetadata(Endpoint endpoint, Dictionary<string, IEdmModel> routes)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(routes);

            foreach (var metadata in endpoint.Metadata)
            {
                if (!TryReadPrefixAndModel(metadata, out var prefix, out var model) || model is null || prefix is null)
                {
                    continue;
                }

                routes[prefix] = model;
            }
        }

        /// <summary>
        /// Reads an OData 7 / Restier catch-all and resolves its model from the per-route container.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="services">The application services.</param>
        /// <param name="routes">The accumulating prefix map.</param>
        internal static void TryAddFromCatchAll(Endpoint endpoint, IServiceProvider services, Dictionary<string, IEdmModel> routes)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(routes);

            if (endpoint is not RouteEndpoint route)
            {
                return;
            }

            if (!TryParseODataCatchAll(route.RoutePattern.RawText, out var prefix, out var routeName))
            {
                return;
            }

            var model = TryGetModelFromPerRouteContainer(services, routeName);
            if (model is not null)
            {
                routes[prefix] = model;
            }
        }

        /// <summary>
        /// Duck-types <c>IOptions&lt;ODataOptions&gt;.Value.RouteComponents</c> when that type is already loaded.
        /// </summary>
        /// <param name="services">The application services.</param>
        /// <param name="routes">The accumulating prefix map.</param>
        internal static void TryAddFromRouteComponents(IServiceProvider services, Dictionary<string, IEdmModel> routes)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(routes);

            var assembly = TryGetLoadedODataAssembly();
            if (assembly is null)
            {
                return;
            }

            var optionsType = assembly.GetType(ODataEightOptionsTypeName) ?? assembly.GetType(ODataSevenOptionsTypeName);
            if (optionsType is null)
            {
                return;
            }

            var ioptions = typeof(IOptions<>).MakeGenericType(optionsType);
            object? wrapped;
            try
            {
                wrapped = services.GetService(ioptions);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (wrapped is null)
            {
                return;
            }

            var value = ioptions.GetProperty("Value")?.GetValue(wrapped);
            var components = value?.GetType().GetProperty("RouteComponents")?.GetValue(value);
            if (components is not IEnumerable enumerable)
            {
                return;
            }

            foreach (var entry in enumerable)
            {
                if (entry is null)
                {
                    continue;
                }

                var entryType = entry.GetType();
                var key = entryType.GetProperty("Key")?.GetValue(entry) as string;
                if (key is null)
                {
                    continue;
                }

                var pairValue = entryType.GetProperty("Value")?.GetValue(entry);
                var model = TryReadModel(pairValue);
                if (model is not null)
                {
                    routes[key] = model;
                }
            }
        }

        /// <summary>
        /// Reads <c>Prefix</c> and <c>Model</c> from a metadata object without taking OData routing types.
        /// </summary>
        /// <param name="metadata">The metadata instance.</param>
        /// <param name="prefix">The prefix when present.</param>
        /// <param name="model">The model when present.</param>
        /// <returns>
        /// <c>true</c> when both members exist.
        /// </returns>
        internal static bool TryReadPrefixAndModel(object metadata, out string? prefix, out IEdmModel? model)
        {
            prefix = null;
            model = null;

            var type = metadata.GetType();
            var prefixProperty = type.GetProperty("Prefix");
            var modelProperty = type.GetProperty("Model");
            if (prefixProperty is null || prefixProperty.PropertyType != typeof(string) || modelProperty is null)
            {
                return false;
            }

            if (!typeof(IEdmModel).IsAssignableFrom(modelProperty.PropertyType))
            {
                return false;
            }

            prefix = prefixProperty.GetValue(metadata) as string;
            model = modelProperty.GetValue(metadata) as IEdmModel;

            return prefix is not null && model is not null;
        }

        /// <summary>
        /// Reads an <see cref="IEdmModel"/> from a RouteComponents value (tuple Item1, Model property, or the value itself).
        /// </summary>
        /// <param name="value">The dictionary value.</param>
        /// <returns>
        /// The model, or <c>null</c>.
        /// </returns>
        internal static IEdmModel? TryReadModel(object? value)
        {
            if (value is IEdmModel model)
            {
                return model;
            }

            if (value is null)
            {
                return null;
            }

            var type = value.GetType();

            return type.GetField("Item1")?.GetValue(value) as IEdmModel
                ?? type.GetProperty("Item1")?.GetValue(value) as IEdmModel
                ?? type.GetProperty("Model")?.GetValue(value) as IEdmModel;
        }

        #endregion

    }

}
