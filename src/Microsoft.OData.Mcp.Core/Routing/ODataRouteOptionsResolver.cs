namespace Microsoft.OData.Mcp.Core.Routing
{

    /// <summary>
    /// Resolves OData route options to determine route patterns.
    /// </summary>
    public class ODataRouteOptionsResolver
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataRouteOptionsResolver"/> class.
        /// </summary>
        public ODataRouteOptionsResolver()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether dollar sign prefixes are enabled for OData routes.
        /// </summary>
        /// <returns>True if dollar prefixes are enabled; otherwise, false.</returns>
        public bool UsesDollarPrefix()
        {
            // Default OData behavior uses dollar prefixes
            return true;
        }

        /// <summary>
        /// Gets the metadata endpoint path based on OData options.
        /// </summary>
        /// <returns>The metadata path.</returns>
        public string GetMetadataPath()
        {
            return UsesDollarPrefix() ? "$metadata" : "metadata";
        }

        /// <summary>
        /// Gets the batch endpoint path based on OData options.
        /// </summary>
        /// <returns>The batch path.</returns>
        public string GetBatchPath()
        {
            return UsesDollarPrefix() ? "$batch" : "batch";
        }

        /// <summary>
        /// Formats a system query option based on dollar prefix settings.
        /// </summary>
        /// <param name="option">The option name without prefix (e.g., "filter", "select").</param>
        /// <returns>The formatted option name.</returns>
        public string FormatQueryOption(string option)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                return option;
            }

            return UsesDollarPrefix() ? $"${option}" : option;
        }

        #endregion

    }

}
