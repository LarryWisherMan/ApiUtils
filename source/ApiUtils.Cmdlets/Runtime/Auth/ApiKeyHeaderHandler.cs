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
    /// Implements API key authentication by sending the key in a custom HTTP header.
    /// </summary>
    /// <remarks>
    /// This handler applies the API key to the request header specified by
    /// <see cref="ApiSessionInfo.ApiKeyHeader"/> (default is <c>X-API-KEY</c>).
    /// </remarks>
    public sealed class ApiKeyHeaderHandler : IAuthHandler
    {
        /// <summary>
        /// Configures the <see cref="HttpClient"/> with the API key header.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="info">The API session information containing the API key.</param>
        /// <param name="ct">A cancellation token (not used).</param>
        /// <returns>A completed task.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ApiSessionInfo.ApiKey"/> is null or whitespace.
        /// </exception>
        public Task AuthenticateAsync(HttpClient client,
                                      ApiSessionInfo info,
                                      CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(info.ApiKey))
                throw new InvalidOperationException("ApiKey is required.");

            client.DefaultRequestHeaders.Remove(info.ApiKeyHeader);
            client.DefaultRequestHeaders.Add(info.ApiKeyHeader, info.ApiKey);

            return Task.CompletedTask;
        }
    }
}
