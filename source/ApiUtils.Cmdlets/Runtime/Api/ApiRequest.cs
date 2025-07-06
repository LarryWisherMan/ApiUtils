#nullable enable
using System;
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ApiUtils.Runtime
{
    /// <summary>
    /// Describes a single HTTP API request with method, URL, headers, content, and cancellation support.
    /// </summary>
    public sealed class ApiRequest
    {
        /// <summary>
        /// The relative or absolute endpoint to call (e.g., "/v1/files").
        /// </summary>
        public string Endpoint { get; set; } = "/";

        /// <summary>
        /// The HTTP method to use (GET, POST, PUT, etc.).
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.Get;

        /// <summary>
        /// Optional body content to send with the request.
        /// </summary>
        public HttpContent? Content { get; set; }

        /// <summary>
        /// Optional additional headers to include in the request.
        /// </summary>
        public Hashtable? Headers { get; set; }

        /// <summary>
        /// Optional cancellation token for the request.
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = default;

        /// <inheritdoc/>
        public override string ToString() => $"{Method} {Endpoint} (HasBody={Content is not null})";
    }

    /// <summary>
    /// Wraps an HTTP response and provides convenience methods to inspect or deserialize the result.
    /// </summary>
    public sealed class ApiResponse : IDisposable, IAsyncDisposable
    {
        private readonly HttpResponseMessage _raw;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiResponse"/> class.
        /// </summary>
        /// <param name="raw">The underlying HTTP response message.</param>
        /// <param name="endpoint">The original request endpoint (for diagnostic purposes).</param>
        public ApiResponse(HttpResponseMessage raw, string endpoint)
        {
            _raw = raw ?? throw new ArgumentNullException(nameof(raw));
            Endpoint = endpoint;
        }

        /// <summary>
        /// Gets the original request endpoint used to obtain this response.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Gets the HTTP status code returned by the server.
        /// </summary>
        public HttpStatusCode StatusCode => _raw.StatusCode;

        /// <summary>
        /// Gets a value indicating whether the status code represents a success (200–299).
        /// </summary>
        public bool IsSuccessful => _raw.IsSuccessStatusCode;

        /// <summary>
        /// Gets the HTTP reason phrase returned by the server.
        /// </summary>
        public string ReasonPhrase => _raw.ReasonPhrase ?? string.Empty;

        /// <summary>
        /// Gets the HTTP response headers.
        /// </summary>
        public HttpResponseHeaders Headers => _raw.Headers;

        /// <summary>
        /// Gets the HTTP content headers, if any.
        /// </summary>
        public HttpContentHeaders? ContentHeaders => _raw.Content?.Headers;

        /// <summary>
        /// Gets the raw underlying <see cref="HttpResponseMessage"/> object.
        /// </summary>
        public HttpResponseMessage Raw => _raw;

        /// <summary>
        /// Throws an exception if the response indicates failure.
        /// </summary>
        /// <returns>The current <see cref="ApiResponse"/> instance (for fluent chaining).</returns>
        /// <exception cref="HttpRequestException">Thrown if the status code is not successful.</exception>
        public ApiResponse EnsureSuccess()
        {
            _raw.EnsureSuccessStatusCode();
            return this;
        }

        /// <summary>
        /// Reads the HTTP response body as a raw UTF-8 string.
        /// </summary>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>The response body as a string.</returns>
        public async Task<string> ReadStringAsync(CancellationToken ct = default)
        {
#if NETFRAMEWORK
            return await _raw.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            return await _raw.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#endif
        }

        /// <summary>
        /// Reads the HTTP response body as a byte array.
        /// </summary>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>The response body as a byte array.</returns>
        public async Task<byte[]> ReadBytesAsync(CancellationToken ct = default)
        {
#if NETFRAMEWORK
            return await _raw.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#else
            return await _raw.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
#endif
        }

        /// <summary>
        /// Deserializes the HTTP response body as a JSON object of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target .NET type to deserialize to.</typeparam>
        /// <param name="opts">Optional serializer options.</param>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A deserialized object of type <typeparamref name="T"/>.</returns>
        public async Task<T?> ReadJsonAsync<T>(JsonSerializerOptions? opts = null, CancellationToken ct = default)
        {
            var json = await ReadStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json)) return default;
            return JsonSerializer.Deserialize<T>(json, opts);
        }

        public Task<object?> ReadJsonObjectAsync(CancellationToken ct = default) =>
    ReadJsonAsync<object>(null, ct);

        public Task<object[]?> ReadJsonArrayAsync(CancellationToken ct = default) =>
            ReadJsonAsync<object[]>(null, ct);

        /// <summary>
        /// Releases all resources used by the underlying <see cref="HttpResponseMessage"/>.
        /// </summary>
        public void Dispose() => _raw.Dispose();

        /// <summary>
        /// Asynchronously disposes the response.
        /// </summary>
        /// <returns>A task representing the disposal operation.</returns>
#if NETFRAMEWORK
        public ValueTask DisposeAsync()
        {
            _raw.Dispose();
            return new ValueTask();
        }
#else
        public ValueTask DisposeAsync()
        {
            _raw.Dispose();
            return ValueTask.CompletedTask;
        }
#endif

        /// <inheritdoc/>
        public override string ToString() =>
            $"HTTP {(int)StatusCode} {StatusCode} — {(IsSuccessful ? "OK" : ReasonPhrase)}";
    }
}
