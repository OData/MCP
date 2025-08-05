namespace Microsoft.OData.Mcp.Sidecar.Services
{
    /// <summary>
    /// Validation information message.
    /// </summary>
    /// <remarks>
    /// Represents informational messages generated during configuration validation.
    /// These messages provide helpful insights about the configuration, such as
    /// detected features, recommendations, best practices, or general information
    /// about how the configuration will be used.
    /// </remarks>
    public sealed class ValidationInfo : ValidationMessage
    {
        #region Properties

        /// <summary>
        /// Gets or sets the information type.
        /// </summary>
        /// <value>The type of information being conveyed.</value>
        /// <remarks>
        /// The information type helps categorize the message and provides context
        /// about what kind of information is being presented to the user.
        /// </remarks>
        public InfoType Type { get; set; } = InfoType.General;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationInfo"/> class.
        /// </summary>
        public ValidationInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationInfo"/> class with the specified message.
        /// </summary>
        /// <param name="message">The information message.</param>
        public ValidationInfo(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationInfo"/> class with the specified message and type.
        /// </summary>
        /// <param name="message">The information message.</param>
        /// <param name="type">The type of information being conveyed.</param>
        public ValidationInfo(string message, InfoType type) : base(message)
        {
            Type = type;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationInfo"/> class with the specified message and path.
        /// </summary>
        /// <param name="message">The information message.</param>
        /// <param name="path">The configuration path where the information applies.</param>
        public ValidationInfo(string message, string path) : base(message, path)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationInfo"/> class with complete information.
        /// </summary>
        /// <param name="message">The information message.</param>
        /// <param name="path">The configuration path where the information applies.</param>
        /// <param name="section">The configuration section name.</param>
        public ValidationInfo(string message, string path, string section) : base(message, path, section)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationInfo"/> class with complete information and type.
        /// </summary>
        /// <param name="message">The information message.</param>
        /// <param name="path">The configuration path where the information applies.</param>
        /// <param name="section">The configuration section name.</param>
        /// <param name="type">The type of information being conveyed.</param>
        public ValidationInfo(string message, string path, string section, InfoType type) 
            : base(message, path, section)
        {
            Type = type;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a general information message.
        /// </summary>
        /// <param name="message">The information message.</param>
        /// <param name="path">The configuration path where the information applies.</param>
        /// <returns>A new general information message.</returns>
        public static ValidationInfo General(string message, string path = "")
        {
            return new ValidationInfo(message, path)
            {
                Type = InfoType.General
            };
        }

        /// <summary>
        /// Creates a recommendation information message.
        /// </summary>
        /// <param name="message">The recommendation message.</param>
        /// <param name="path">The configuration path where the recommendation applies.</param>
        /// <returns>A new recommendation information message.</returns>
        public static ValidationInfo Recommendation(string message, string path = "")
        {
            return new ValidationInfo(message, path)
            {
                Type = InfoType.Recommendation
            };
        }

        /// <summary>
        /// Creates a best practice information message.
        /// </summary>
        /// <param name="message">The best practice message.</param>
        /// <param name="path">The configuration path where the best practice applies.</param>
        /// <returns>A new best practice information message.</returns>
        public static ValidationInfo BestPractice(string message, string path = "")
        {
            return new ValidationInfo(message, path)
            {
                Type = InfoType.BestPractice
            };
        }

        /// <summary>
        /// Creates a feature information message.
        /// </summary>
        /// <param name="message">The feature information message.</param>
        /// <param name="path">The configuration path where the feature applies.</param>
        /// <returns>A new feature information message.</returns>
        public static ValidationInfo Feature(string message, string path = "")
        {
            return new ValidationInfo(message, path)
            {
                Type = InfoType.Feature
            };
        }

        /// <summary>
        /// Returns a string representation of the validation information.
        /// </summary>
        /// <returns>A formatted string containing the information.</returns>
        public override string ToString()
        {
            var baseString = base.ToString();
            var typeIndicator = Type switch
            {
                InfoType.Recommendation => "[RECOMMENDATION] ",
                InfoType.BestPractice => "[BEST PRACTICE] ",
                InfoType.Feature => "[FEATURE] ",
                _ => ""
            };
            
            return $"{typeIndicator}{baseString}";
        }

        #endregion
    }
}