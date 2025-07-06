#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Models;
using System.Management.Automation;     // for ScriptBlock

namespace ApiUtils.Runtime.Auth
{
    // ────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Executes a user-supplied delegate to perform arbitrary authentication.
    /// </summary>
    internal sealed class DelegatingAuthHandler : IAuthHandler
    {
        private readonly Func<HttpClient, ApiSessionInfo, CancellationToken, Task> _action;

        public DelegatingAuthHandler(
            Func<HttpClient, ApiSessionInfo, CancellationToken, Task> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public Task AuthenticateAsync(HttpClient client,
                                      ApiSessionInfo info,
                                      CancellationToken ct = default)
            => _action(client, info, ct);
    }

    // ────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Invokes a PowerShell <see cref="ScriptBlock"/> to apply authentication.
    /// </summary>
    internal sealed class ScriptAuthHandler : IAuthHandler
    {
        private readonly ScriptBlock _script;

        public ScriptAuthHandler(ScriptBlock script)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
        }

        public Task AuthenticateAsync(HttpClient client,
                                      ApiSessionInfo info,
                                      CancellationToken ct = default)
        {
            _script.InvokeReturnAsIs(client, info, ct);
            return Task.CompletedTask;
        }
    }

    // ────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Injects a set of static headers into every outgoing request.
    /// </summary>
    internal sealed class HeaderInjectionHandler : IAuthHandler
    {
        private readonly IReadOnlyDictionary<string, string> _headers;

        public HeaderInjectionHandler(IReadOnlyDictionary<string, string> headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        public Task AuthenticateAsync(HttpClient client,
                                      ApiSessionInfo info,
                                      CancellationToken ct = default)
        {
            foreach (var kvp in _headers)
            {
                client.DefaultRequestHeaders.Remove(kvp.Key);
                client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }
            return Task.CompletedTask;
        }
    }
}
