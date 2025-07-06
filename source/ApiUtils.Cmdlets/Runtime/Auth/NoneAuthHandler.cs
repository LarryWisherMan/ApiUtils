#nullable enable
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Models;

namespace ApiUtils.Runtime.Auth
{
    //───────────────────────────────────────────────────────────────────────
    /// <summary>
    /// “No authentication” strategy.  Requests are sent exactly as-is.
    /// </summary>
    /// <remarks>
    /// Useful for public or development endpoints that do not require any
    /// form of authorization header, API key, or token.
    /// </remarks>
    internal sealed class NoneAuthHandler : IAuthHandler
    {
        /// <inheritdoc/>
        public Task AuthenticateAsync(HttpClient client,
                                      ApiSessionInfo info,
                                      CancellationToken ct = default)
        {
            // Intentionally no-op.
            return Task.CompletedTask;
        }
    }
}
