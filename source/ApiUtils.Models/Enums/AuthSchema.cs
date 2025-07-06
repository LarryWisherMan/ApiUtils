namespace ApiUtils.Models
{
    /// <summary>
    /// Identifies the authentication strategy that an <c>IAuthHandler</c> will
    /// apply to an <c>ApiSession</c>.
    ///
    /// The enum deliberately uses consecutive integers (no gaps) so it can be
    /// serialized/round-tripped without a custom converter.
    /// </summary>
    public enum AuthScheme
    {
        /// <summary>
        /// No authentication.  Requests are sent exactly as constructed.
        /// </summary>
        None = 0,

        /// <summary>
        /// HTTP Basic authentication (<c>Authorization: Basic …</c>).
        /// </summary>
        Basic = 1,

        /// <summary>
        /// Static API key supplied either as a header
        /// (<c>X-API-KEY</c>) or query-string parameter.
        /// </summary>
        ApiKey = 2,

        /// <summary>
        /// Pre-issued bearer/JWT token supplied in the
        /// <c>Authorization: Bearer …</c> header.
        /// </summary>
        Bearer = 3,

        /// <summary>
        /// OAuth 2.0 flow (client-credentials, auth-code, etc.) in which
        /// a token must first be obtained from an authorization endpoint.
        /// </summary>
        OAuth2 = 4,

        /// <summary>
        /// A caller-supplied delegate or script block performs all
        /// authentication work (“escape hatch” for bespoke scenarios).
        /// </summary>
        Custom = 5
    }
}
