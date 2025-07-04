namespace ApiUtils.Models
{
    /// <summary>
    /// Enumeration of supported authentication types
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// No authentication
        /// </summary>
        None,

        /// <summary>
        /// Basic authentication using username and password
        /// </summary>
        Basic,

        /// <summary>
        /// Bearer token authentication
        /// </summary>
        Bearer,

        /// <summary>
        /// API key authentication
        /// </summary>
        ApiKey,

        /// <summary>
        /// OAuth 2.0 authentication
        /// </summary>
        OAuth2,

        /// <summary>
        /// Custom authentication scheme
        /// </summary>
        Custom
    }
}
