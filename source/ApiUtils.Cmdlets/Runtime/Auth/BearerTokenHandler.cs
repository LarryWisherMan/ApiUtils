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
    /// Implements Bearer token authentication by applying a pre-issued token to the Authorization header.
    /// </summary>
    /// <remarks>
    /// This handler expects the <see cref="ApiSessionInfo.Token"/> property to contain a valid bearer token.
    /// If the token is missing or empty, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public sealed class BearerTokenHandler : IAuthHandler
    {
        /// <summary>
        /// Applies a Bearer token to the given <see cref="HttpClient"/> instance.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="info">The API session info containing the token.</param>
        /// <param name="ct">A cancellation token (not used in this implementation).</param>
        /// <returns>A completed task.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the <see cref="ApiSessionInfo.Token"/> property is null, empty, or whitespace.
        /// </exception>
        public Task AuthenticateAsync(HttpClient client,
                                      ApiSessionInfo info,
                                      CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(info.Token))
                throw new InvalidOperationException("Token must be provided for Bearer auth.");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", info.Token);

            return Task.CompletedTask;
        }
    }
}
