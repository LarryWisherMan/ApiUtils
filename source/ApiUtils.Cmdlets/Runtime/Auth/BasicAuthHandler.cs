#nullable enable
using System;
using System.Text;
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
    /// Implements HTTP <c>Basic</c> authentication by adding an Authorization header
    /// with a base64-encoded username and password combination.
    /// </summary>
    /// <remarks>
    /// This handler expects valid credentials in the <see cref="ApiSessionInfo.Credential"/> property.
    /// If credentials are missing, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public sealed class BasicAuthHandler : IAuthHandler
    {
        /// <summary>
        /// Adds a Basic authentication header to the provided <see cref="HttpClient"/> instance.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="info">The API session information containing credentials.</param>
        /// <param name="ct">A cancellation token (not used in this implementation).</param>
        /// <returns>A completed task.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="ApiSessionInfo.Credential"/> is null.
        /// </exception>
        public Task AuthenticateAsync(HttpClient client,
                                      ApiSessionInfo info,
                                      CancellationToken ct = default)
        {
            if (info.Credential is null)
                throw new InvalidOperationException("Basic auth requires Credential.");

            var token = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    $"{info.Credential.UserName}:{info.Credential.GetNetworkCredential().Password}"));

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", token);

            return Task.CompletedTask;
        }
    }
}
