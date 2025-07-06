#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation;
using ApiUtils.Models; // for AuthScheme enum

namespace ApiUtils.Models
{
    /// <summary>
    /// Immutable configuration record that describes how to authenticate and reach a
    /// particular REST-style API endpoint.
    /// The record is <b>data-only</b>; it contains no behavioral logic.  All fields are
    /// consumed by an <c>IAuthHandler</c> implementation at runtime.
    /// </summary>
    /// <param name="Name">
    /// A logical, <b>unique</b> name for the session (e.g. “GitHub”, “Axway-Prod”).
    /// Used as the key inside <c>SessionRegistry</c>.
    /// </param>
    /// <param name="BaseUri">
    /// The absolute base URI of the service, e.g. <c>https://api.example.com</c>.
    /// </param>
    /// <param name="Scheme">
    /// The authentication scheme that should be applied.  Typical values are
    /// <c>None</c>, <c>Basic</c>, <c>ApiKey</c>, <c>Bearer</c>, or <c>OAuth2</c>.
    /// </param>
    /// <param name="Credential">
    /// PowerShell credential object (username + secure password) used by
    /// <c>Basic</c> or certain OAuth flows.
    /// </param>
    /// <param name="ApiKey">
    /// Raw API-key string used by <c>ApiKey</c> handlers (header or query).
    /// </param>
    /// <param name="ApiKeyHeader">
    /// Header name to carry <paramref name="ApiKey"/> when the key is sent as a
    /// request header.  Default is <c>X-API-KEY</c>.
    /// </param>
    /// <param name="ApiKeyQueryParam">
    /// Query-string parameter used when the API-key is appended to the URI
    /// (e.g. <c>?key=123</c>).  Default is <c>key</c>.
    /// </param>
    /// <param name="Token">
    /// A pre-issued bearer / JWT token for <c>Bearer</c> authentication.
    /// </param>
    /// <param name="AuthEndpoint">
    /// OAuth 2.0 / custom endpoint that can be called to exchange credentials for a
    /// token.  Used by <c>BearerTokenHandler</c> or <c>OAuth2*</c> handlers.
    /// </param>
    /// <param name="ClientId">
    /// OAuth 2.0 client-ID (for client-credentials or auth-code flows).
    /// </param>
    /// <param name="ClientSecret">
    /// OAuth 2.0 client-secret corresponding to <paramref name="ClientId"/>.
    /// </param>
    /// <param name="Scope">
    /// OAuth 2.0 scope string requested when acquiring a token.
    /// </param>
    /// <param name="Headers">
    /// A dictionary of static headers that should be injected into every request
    /// (e.g. <c>X-Tenant</c>, <c>X-Version</c>).
    /// </param>
    /// <param name="CustomAuthenticator">
    /// A delegate that performs arbitrary authentication logic.  It receives the
    /// <see cref="HttpClient"/>, the current <see cref="ApiSessionInfo"/>, and an
    /// optional <see cref="CancellationToken"/>.  When supplied, the runtime will
    /// wrap it with a <c>DelegatingAuthHandler</c>.
    /// </param>
    /// <param name="ScriptAuthenticator">
    /// PowerShell <see cref="ScriptBlock"/> that performs authentication when you
    /// prefer to write the handshake in PowerShell instead of C#.  Executed inside
    /// the calling runspace.
    /// </param>
    public sealed record ApiSessionInfo(
        string Name,
        Uri BaseUri,
        AuthScheme Scheme = AuthScheme.None,
        PSCredential? Credential = null,
        string? ApiKey = null,
        string ApiKeyHeader = "X-API-KEY",
        string ApiKeyQueryParam = "key",
        string? Token = null,
        Uri? AuthEndpoint = null,
        string? ClientId = null,
        string? ClientSecret = null,
        string? Scope = null,
        IReadOnlyDictionary<string, string>? Headers = null,
        Func<HttpClient, ApiSessionInfo, CancellationToken, Task>? CustomAuthenticator = null,
        ScriptBlock? ScriptAuthenticator = null
    );
}
