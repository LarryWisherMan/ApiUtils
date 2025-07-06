using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Models;

namespace ApiUtils.Cmdlets;

/// <summary>
/// Constructs an immutable <see cref="ApiSessionInfo"/> used for connecting to an HTTP API.
/// This cmdlet performs no network traffic or validation. It only packages configuration metadata.
/// </summary>
/// <remarks>
/// Supports various authentication schemes via parameter sets:
/// <list type="bullet">
///   <item><term>Basic</term> - username/password in HTTP Authorization header</item>
///   <item><term>Bearer</term> - pre-issued token added to Authorization header</item>
///   <item><term>OAuth2</term> - client credentials flow using a token endpoint</item>
///   <item><term>ApiKey</term> - custom header injection (e.g. X-API-KEY)</item>
///   <item><term>Custom</term> - manual injection via ScriptBlock or header table</item>
/// </list>
/// </remarks>
[Cmdlet(VerbsCommon.New, "ApiSession", SupportsShouldProcess = false, DefaultParameterSetName = "None")]
[OutputType(typeof(ApiSessionInfo))]
public sealed class NewApiSessionCommand : PSCmdlet
{
    /// <summary>
    /// Logical name for the session. This acts as a unique key when storing or retrieving from a registry.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "Logical name for the session (unique key).")]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The base URI of the REST API service.
    /// </summary>
    /// <example>https://api.example.com</example>
    [Parameter(Mandatory = true, Position = 1, HelpMessage = "Base URI of the REST API (e.g. https://api.example.com).")]
    public Uri BaseUri { get; set; } = new Uri("https://api.example.com");

    /// <summary>
    /// The authentication scheme used to select the appropriate handler.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "None")]
    [Parameter(Mandatory = true, ParameterSetName = "Basic")]
    [Parameter(Mandatory = true, ParameterSetName = "Bearer")]
    [Parameter(Mandatory = true, ParameterSetName = "OAuth2")]
    [Parameter(Mandatory = true, ParameterSetName = "ApiKey")]
    [Parameter(Mandatory = true, ParameterSetName = "Custom")]
    public AuthScheme Scheme { get; set; }


    //──────────────────────────────────────────────────────────────────
    // Shared (Basic / OAuth2) Credential
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Username and password pair for Basic authentication or client credentials for OAuth2.
    /// </summary>
    [Parameter(ParameterSetName = "Basic", Mandatory = true)]
    public PSCredential? Credential { get; set; }

    //──────────────────────────────────────────────────────────────────
    // API Key Authentication
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// API key to send in a custom HTTP header.
    /// </summary>
    [Parameter(ParameterSetName = "ApiKey")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// The name of the HTTP header used to transmit the API key.
    /// </summary>
    /// <remarks>Defaults to <c>X-API-KEY</c>.</remarks>
    [Parameter(ParameterSetName = "ApiKey")]
    public string ApiKeyHeader { get; set; } = "X-API-KEY";

    //──────────────────────────────────────────────────────────────────
    // Bearer Token Authentication
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pre-issued bearer or JWT token used directly in Authorization header.
    /// </summary>
    [Parameter(ParameterSetName = "Bearer", Mandatory = true)]
    public string? Token { get; set; }

    //──────────────────────────────────────────────────────────────────
    // OAuth2: Client Credentials Flow
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Token endpoint URI used to request an OAuth2 access token.
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    public Uri? AuthEndpoint { get; set; }

    /// <summary>
    /// OAuth2 client ID (typically a GUID or application name).
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    public string? ClientId { get; set; }

    /// <summary>
    /// OAuth2 client secret.
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Optional space-delimited list of OAuth scopes to request.
    /// </summary>
    [Parameter(ParameterSetName = "OAuth2")]
    public string? Scope { get; set; }

    //──────────────────────────────────────────────────────────────────
    // Custom authentication extensions
    //──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A PowerShell <see cref="ScriptBlock"/> that receives (HttpClient, ApiSessionInfo, CancellationToken).
    /// Use this for custom authentication logic (e.g., call an internal token broker).
    /// </summary>
    [Parameter(ParameterSetName = "Custom")]
    public ScriptBlock? CustomAuthenticator { get; set; }

    /// <summary>
    /// Key-value pairs to inject into the request headers (e.g., Auth headers, tenant IDs, etc.).
    /// </summary>
    [Parameter(ParameterSetName = "Custom")]
    public Hashtable? Headers { get; set; }

    /// <summary>
    /// Optional legacy-style script handler (used for compatibility scenarios).
    /// </summary>
    [Parameter(ParameterSetName = "Custom")]
    public ScriptBlock? ScriptAuthenticator { get; set; }

    //──────────────────────────────────────────────────────────────────
    // Cmdlet logic
    //──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        Dictionary<string, string>? hdrs = null;

        if (Headers is not null)
        {
            var tmp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry kv in Headers)
            {
                // Null-safe conversion with fallback
                var key = kv.Key as string ?? throw new InvalidCastException("Header key must be a non-null string.");
                var val = kv.Value as string ?? throw new InvalidCastException($"Header value for '{key}' must be a non-null string.");

                tmp[key] = val;
            }
            hdrs = tmp;
        }

        var info = new ApiSessionInfo(
            Name,
            BaseUri,
            Scheme,
            Credential,
            ApiKey,
            ApiKeyHeader,
            /* ApiKeyQueryParam */ "key",
            Token,
            AuthEndpoint,
            ClientId,
            ClientSecret,
            Scope,
            hdrs,
            CustomAuthenticator is null
                ? null
                : new Func<HttpClient, ApiSessionInfo, CancellationToken, Task>(
                      (c, i, ct) =>
                      {
                          CustomAuthenticator.Invoke(c, i, ct);
                          return Task.CompletedTask;
                      }),
            ScriptAuthenticator);

        WriteObject(info);
    }


}
