namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Information message types.
    /// </summary>
    /// <remarks>
    /// Defines the types of informational messages that can be generated
    /// during configuration validation. These categories help users understand
    /// the nature and purpose of the information being provided.
    /// </remarks>
    public enum InfoType
    {
        /// <summary>
        /// General information.
        /// </summary>
        /// <remarks>
        /// General informational messages provide basic information about
        /// the configuration, detected settings, or operational details
        /// that may be useful for understanding how the service will behave.
        /// </remarks>
        General,

        /// <summary>
        /// Configuration recommendation.
        /// </summary>
        /// <remarks>
        /// Recommendation messages suggest specific configuration changes
        /// or settings that could improve performance, security, or
        /// functionality. These are actionable suggestions based on
        /// best practices or detected usage patterns.
        /// </remarks>
        Recommendation,

        /// <summary>
        /// Best practice suggestion.
        /// </summary>
        /// <remarks>
        /// Best practice messages highlight opportunities to align the
        /// configuration with established best practices for security,
        /// performance, maintainability, or operational excellence.
        /// </remarks>
        BestPractice,

        /// <summary>
        /// Feature information.
        /// </summary>
        /// <remarks>
        /// Feature messages provide information about detected or configured
        /// features, their status, capabilities, or requirements. These help
        /// users understand what functionality will be available.
        /// </remarks>
        Feature
    }
}