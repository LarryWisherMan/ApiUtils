#nullable enable
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Models;

namespace ApiUtils.Runtime.Auth
{
    /// <summary>
    /// Defines a pluggable strategy that applies authentication
    /// (headers, tokens, signatures, etc.) to an <see cref="HttpClient"/>
    /// before the client sends a request.
    ///
    /// <para>
    /// Implementations must be <b>stateless</b> or manage their own
    /// synchronization, because a single handler instance may be used
    /// concurrently by multiple threads when an <see cref="ApiSession"/>
    /// is shared in parallel jobs.
    /// </para>
    /// </summary>
    public interface IAuthHandler
    {
        /// <summary>
        /// Ensures the supplied <paramref name="client"/> carries valid
        /// authentication information for the API described by
        /// <paramref name="info"/>.
        /// </summary>
        /// <param name="client">
        /// The <see cref="HttpClient"/> owned by an <c>ApiSession</c>.
        /// Implementations typically modify <c>DefaultRequestHeaders</c>
        /// or refresh bearer tokens.
        /// </param>
        /// <param name="info">
        /// Immutable session metadata containing credentials, API key,
        /// token endpoint, and other data required by the handler.
        /// </param>
        /// <param name="ct">
        /// Optional cancellation token propagated from the caller.
        /// </param>
        /// <returns>
        /// A task that completes when authentication headers are applied.
        /// Implementations can return <see cref="Task.CompletedTask"/> if
        /// no asynchronous work is necessary (e.g., static API key).
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when required fields (e.g., credentials) are missing.
        /// </exception>
        Task AuthenticateAsync(
            HttpClient client,
            ApiSessionInfo info,
            CancellationToken ct = default);
    }
}
