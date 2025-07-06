#nullable enable
using System;
using ApiUtils.Models;

namespace ApiUtils.Runtime.Auth
{
    /// <summary>
    /// Produces the appropriate <see cref="IAuthHandler"/> for a given
    /// <see cref="ApiSessionInfo"/> instance.
    /// </summary>
    internal static class AuthHandlerFactory
    {
        /// <summary>
        /// Returns an <see cref="IAuthHandler"/> that can satisfy the authentication
        /// requirements described by <paramref name="info"/>.
        /// </summary>
        /// <param name="info">Immutable session metadata.</param>
        /// <returns>An authentication handler ready for use.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <see cref="ApiSessionInfo.Scheme"/> is not recognised.
        /// </exception>
        public static IAuthHandler Create(ApiSessionInfo info) => info.Scheme switch
        {
            AuthScheme.None => new NoneAuthHandler(),

            AuthScheme.Basic => new BasicAuthHandler(),

            AuthScheme.ApiKey => new ApiKeyHeaderHandler(), // header-based; add query variant later

            AuthScheme.Bearer => new BearerTokenHandler(),

            AuthScheme.OAuth2 => new OAuth2ClientCredsHandler(),

            AuthScheme.Custom => CreateCustomHandler(info),

            _ => throw new NotSupportedException(
                     $"Authentication scheme '{info.Scheme}' is not supported.")
        };

        // ── helpers ──────────────────────────────────────────────────────
        private static IAuthHandler CreateCustomHandler(ApiSessionInfo info)
        {
            if (info.CustomAuthenticator is not null)
                return new DelegatingAuthHandler(info.CustomAuthenticator);

            if (info.ScriptAuthenticator is not null)
                return new ScriptAuthHandler(info.ScriptAuthenticator);

            if (info.Headers is not null && info.Headers.Count > 0)
                return new HeaderInjectionHandler(info.Headers);

            throw new InvalidOperationException(
                "Custom auth selected, but no delegate, script block, or headers provided.");
        }
    }

    
}
