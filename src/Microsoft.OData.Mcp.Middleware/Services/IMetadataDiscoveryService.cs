using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Middleware.Services
{
    /// <summary>
    /// Service for discovering and managing OData metadata within the middleware.
    /// </summary>
    /// <remarks>
    /// This service handles automatic discovery of OData metadata from the host application,
    /// caching of parsed models, and notifications when metadata changes.
    /// </remarks>
    public interface IMetadataDiscoveryService
    {
        /// <summary>
        /// Event raised when metadata is discovered or updated.
        /// </summary>
        event EventHandler<MetadataDiscoveredEventArgs>? MetadataDiscovered;

        /// <summary>
        /// Event raised when metadata discovery fails.
        /// </summary>
        event EventHandler<MetadataDiscoveryFailedEventArgs>? MetadataDiscoveryFailed;

        /// <summary>
        /// Gets the current OData model, if available.
        /// </summary>
        /// <value>The current OData model, or null if not yet discovered.</value>
        EdmModel? CurrentModel { get; }

        /// <summary>
        /// Gets the timestamp when the current model was last updated.
        /// </summary>
        /// <value>The UTC timestamp of the last model update, or null if no model is available.</value>
        DateTime? LastUpdated { get; }

        /// <summary>
        /// Gets a value indicating whether metadata discovery is currently in progress.
        /// </summary>
        /// <value><c>true</c> if discovery is in progress; otherwise, <c>false</c>.</value>
        bool IsDiscovering { get; }

        /// <summary>
        /// Starts the metadata discovery service.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous start operation.</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the metadata discovery service.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous stop operation.</returns>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually triggers metadata discovery.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous discovery operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service is not started.</exception>
        Task DiscoverMetadataAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually sets the OData model.
        /// </summary>
        /// <param name="model">The OData model to set.</param>
        /// <param name="source">The source of the model (e.g., "Manual", "Discovery").</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        void SetModel(EdmModel model, string source = "Manual");

        /// <summary>
        /// Clears the current model and forces re-discovery.
        /// </summary>
        void ClearModel();

        /// <summary>
        /// Validates the current model for completeness and correctness.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the model is valid.</returns>
        IEnumerable<string> ValidateCurrentModel();

        /// <summary>
        /// Gets metadata statistics for monitoring and diagnostics.
        /// </summary>
        /// <returns>A dictionary containing metadata statistics.</returns>
        Dictionary<string, object> GetStatistics();
    }

    /// <summary>
    /// Event arguments for metadata discovery events.
    /// </summary>
    public sealed class MetadataDiscoveredEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the discovered OData model.
        /// </summary>
        /// <value>The OData model that was discovered.</value>
        public required EdmModel Model { get; init; }

        /// <summary>
        /// Gets the source of the metadata.
        /// </summary>
        /// <value>A description of where the metadata was discovered from.</value>
        public required string Source { get; init; }

        /// <summary>
        /// Gets the timestamp when the metadata was discovered.
        /// </summary>
        /// <value>The UTC timestamp of the discovery.</value>
        public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets a value indicating whether this is an update to existing metadata.
        /// </summary>
        /// <value><c>true</c> if this is an update; <c>false</c> if this is the initial discovery.</value>
        public bool IsUpdate { get; init; }

        /// <summary>
        /// Gets additional context information about the discovery.
        /// </summary>
        /// <value>A dictionary of additional context data.</value>
        public Dictionary<string, object> Context { get; init; } = new();
    }

    /// <summary>
    /// Event arguments for metadata discovery failure events.
    /// </summary>
    public sealed class MetadataDiscoveryFailedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exception that caused the discovery failure.
        /// </summary>
        /// <value>The exception that occurred during discovery.</value>
        public required Exception Exception { get; init; }

        /// <summary>
        /// Gets the source that was being queried when the failure occurred.
        /// </summary>
        /// <value>A description of the metadata source.</value>
        public required string Source { get; init; }

        /// <summary>
        /// Gets the timestamp when the failure occurred.
        /// </summary>
        /// <value>The UTC timestamp of the failure.</value>
        public DateTime FailedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the number of retry attempts that have been made.
        /// </summary>
        /// <value>The number of retry attempts.</value>
        public int RetryAttempt { get; init; }

        /// <summary>
        /// Gets a value indicating whether discovery will be retried.
        /// </summary>
        /// <value><c>true</c> if discovery will be retried; otherwise, <c>false</c>.</value>
        public bool WillRetry { get; init; }

        /// <summary>
        /// Gets additional context information about the failure.
        /// </summary>
        /// <value>A dictionary of additional context data.</value>
        public Dictionary<string, object> Context { get; init; } = new();
    }
}