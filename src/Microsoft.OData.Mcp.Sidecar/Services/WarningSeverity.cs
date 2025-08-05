namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Warning severity levels.
    /// </summary>
    /// <remarks>
    /// Defines the severity levels for validation warnings to help users
    /// prioritize which issues should be addressed first. Higher severity
    /// warnings typically indicate potential security, performance, or
    /// stability issues.
    /// </remarks>
    public enum WarningSeverity
    {
        /// <summary>
        /// Low severity warning.
        /// </summary>
        /// <remarks>
        /// Low severity warnings are minor issues that have minimal impact
        /// on functionality or performance. These can typically be addressed
        /// at a convenient time and may include style or best practice
        /// recommendations.
        /// </remarks>
        Low,

        /// <summary>
        /// Medium severity warning.
        /// </summary>
        /// <remarks>
        /// Medium severity warnings indicate issues that should be addressed
        /// in a reasonable timeframe. These may affect performance, usability,
        /// or maintainability but don't pose immediate risks.
        /// </remarks>
        Medium,

        /// <summary>
        /// High severity warning.
        /// </summary>
        /// <remarks>
        /// High severity warnings indicate serious issues that should be
        /// addressed as soon as possible. These may affect security,
        /// stability, or core functionality and could lead to problems
        /// in production environments.
        /// </remarks>
        High
    }
}