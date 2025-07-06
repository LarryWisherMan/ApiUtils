#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Models;

namespace ApiUtils.Runtime.Auth
{
    //───────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Implements the OAuth 2.0 <c>client_credentials</c> flow for authentication.
    /// </summary>
    /// <remarks>
    /// This handler retrieves and caches a bearer access token using the configured
    /// <see cref="ApiSessionInfo.AuthEndpoint"/>, <see cref="ApiSessionInfo.ClientId"/>,
    /// and <see cref="ApiSessionInfo.ClientSecret"/>.
    /// </remarks>
    public sealed class OAuth2ClientCredsHandler : IAuthHandler
    {
        private string? _accessToken;
        private DateTimeOffset _expiresUtc;

        /// <summary>
        /// Authenticates the given <see cref="HttpClient"/> by applying a valid
        /// bearer access token obtained through the OAuth 2.0 client credentials flow.
        /// </summary>
        /// <param name="client">The HTTP client to apply authentication headers to.</param>
        /// <param name="info">The session information containing client credentials and auth settings.</param>
        /// <param name="ct">A cancellation token for async operations.</param>
        /// <returns>A task representing the asynchronous authentication operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if required configuration (e.g., <c>AuthEndpoint</c>, <c>ClientId</c>, or <c>ClientSecret</c>) is missing.
        /// </exception>
        public async Task AuthenticateAsync(HttpClient client,
                                            ApiSessionInfo info,
                                            CancellationToken ct = default)
        {
            if (_accessToken is null || DateTimeOffset.UtcNow >= _expiresUtc)
                await RefreshAsync(info, ct).ConfigureAwait(false);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        /// <summary>
        /// Refreshes the cached access token by issuing a client credentials token request.
        /// </summary>
        /// <param name="info">The API session containing OAuth credentials.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the async token refresh operation.</returns>
        private async Task RefreshAsync(ApiSessionInfo info, CancellationToken ct)
        {
            if (info.AuthEndpoint is null)
                throw new InvalidOperationException("AuthEndpoint required for OAuth2.");

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",    "client_credentials"),
                new KeyValuePair<string,string>("client_id",     info.ClientId  ?? throw new InvalidOperationException("ClientId required.")),
                new KeyValuePair<string,string>("client_secret", info.ClientSecret ?? throw new InvalidOperationException("ClientSecret required.")),
                new KeyValuePair<string,string>("scope",         info.Scope ?? string.Empty)
            });

            using var http = new HttpClient();
            using var resp = await http.PostAsync(info.AuthEndpoint, body, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            // Read JSON response cross-platform
#if NETFRAMEWORK
            using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
#endif

            var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = json.RootElement;

            _accessToken = root.GetProperty("access_token").GetString()
                           ?? throw new InvalidOperationException("access_token missing.");

            var expiresIn = root.TryGetProperty("expires_in", out var e)
                               ? e.GetInt32()
                               : 3600;

            _expiresUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30); // safety margin
        }
    }
}
