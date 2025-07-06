#nullable enable
using System;
using System.Collections;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Models;
using ApiUtils.Runtime.Auth;

namespace ApiUtils.Runtime;

/// <summary>
/// Represents a live, reusable, and thread-safe API session that handles
/// authentication, request dispatching, and lifecycle management.
/// </summary>
public sealed class ApiSession : IDisposable
#if !NETFRAMEWORK
    , IAsyncDisposable
#endif
{
    //──────────────────────── Public immutable data ──────────────────────

    /// <summary>
    /// A unique identifier for this session instance, useful for diagnostics and correlation.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The session configuration originally used to create this session.
    /// </summary>
    public ApiSessionInfo Info { get; }

    /// <summary>
    /// The most recent time this session was used to send an API request.
    /// </summary>
    public DateTime LastAccess { get; private set; } = DateTime.UtcNow;

    //──────────────────────── Private state ──────────────────────────────

    private readonly HttpClient _client;
    private readonly IAuthHandler _auth;
    private bool _disposed;

    //──────────────────────── Constructor ────────────────────────────────

    /// <summary>
    /// Initializes a new <see cref="ApiSession"/> using the specified configuration.
    /// </summary>
    /// <param name="info">The session definition including endpoint, authentication method, etc.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="info"/> is null.</exception>
    public ApiSession(ApiSessionInfo info)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _client = new HttpClient { BaseAddress = info.BaseUri };
        _auth = AuthHandlerFactory.Create(info);
    }

    //──────────────────────── Public helpers ─────────────────────────────

    /// <summary>
    /// Sends a simple GET request to the given relative URI and returns the response as a UTF-8 string.
    /// </summary>
    /// <param name="relative">The relative URI to send the GET request to.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>The body of the response as a string.</returns>
    public async Task<string> GetStringAsync(string relative, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await _auth.AuthenticateAsync(_client, Info, ct).ConfigureAwait(false);

#if NETFRAMEWORK
        var result = await _client.GetStringAsync(relative).ConfigureAwait(false);
#else
        var result = await _client.GetStringAsync(relative, ct).ConfigureAwait(false);
#endif
        LastAccess = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Sends a complete <see cref="ApiRequest"/> and returns a rich <see cref="ApiResponse"/> with helpers for reading the result.
    /// </summary>
    /// <param name="request">The API request to send.</param>
    /// <returns>The result of the request wrapped in an <see cref="ApiResponse"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="request"/> is null.</exception>
    public async Task<ApiResponse> SendAsync(ApiRequest request)
    {
        ThrowIfDisposed();
        if (request is null) throw new ArgumentNullException(nameof(request));

        await _auth.AuthenticateAsync(_client, Info, request.CancellationToken).ConfigureAwait(false);

        using var msg = new HttpRequestMessage(request.Method, request.Endpoint)
        {
            Content = request.Content
        };

        if (request.Headers is not null)
        {
            foreach (DictionaryEntry entry in request.Headers)
                msg.Headers.TryAddWithoutValidation(entry.Key.ToString()!, entry.Value!.ToString());
        }

        var httpResp = await _client.SendAsync(
            msg,
            HttpCompletionOption.ResponseHeadersRead,
            request.CancellationToken).ConfigureAwait(false);

        LastAccess = DateTime.UtcNow;
        return new ApiResponse(httpResp, request.Endpoint);
    }

    /// <summary>
    /// Sends a request using direct parameters instead of building an <see cref="ApiRequest"/>.
    /// </summary>
    /// <param name="endpoint">The relative or absolute endpoint to call.</param>
    /// <param name="method">The HTTP method (GET, POST, etc.).</param>
    /// <param name="content">Optional HTTP content (body).</param>
    /// <param name="headers">Optional additional headers.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A wrapped <see cref="ApiResponse"/> with full response access.</returns>
    public Task<ApiResponse> SendAsync(
        string endpoint,
        HttpMethod method,
        HttpContent? content = null,
        Hashtable? headers = null,
        CancellationToken ct = default) =>
        SendAsync(new ApiRequest
        {
            Endpoint = endpoint,
            Method = method,
            Content = content,
            Headers = headers,
            CancellationToken = ct
        });

    //──────────────────────── Disposal pattern ───────────────────────────

    /// <summary>
    /// Releases all managed resources used by the session.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Asynchronously releases resources used by the session.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        Dispose(disposing: true);
        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }
#endif

    /// <summary>
    /// Internal helper that disposes of the underlying <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="disposing">True if called from Dispose; false if finalizer.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) _client.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Throws if this session has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the session has already been disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ApiSession));
    }
}
