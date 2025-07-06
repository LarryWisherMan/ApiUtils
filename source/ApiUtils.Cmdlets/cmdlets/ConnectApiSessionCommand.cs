#nullable enable
using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Models;
using ApiUtils.Runtime;

namespace ApiUtils.Cmdlets;

/// <summary>
/// Establishes an authenticated <see cref="ApiSession"/>, validates connectivity, and registers the session for reuse.
/// </summary>
/// <remarks>
/// This implementation avoids the (non‑existent) <c>AsyncCmdlet</c> base class and instead keeps all <c>Write*</c> calls on the pipeline thread. We still call the asynchronous REST methods, but we <em>block</em> on them inside <c>ProcessRecord()</c> so execution returns to the same thread before touching PowerShell’s output streams.
/// </remarks>
[Cmdlet(VerbsCommunications.Connect, "ApiSession", DefaultParameterSetName = "ByValue", SupportsShouldProcess = true)]
[OutputType(typeof(ApiSession))]
public sealed class ConnectApiSessionCommand : PSCmdlet
{
    //──────────────────────── Parameters ────────────────────────

    /// <summary>Full session definition passed by value or pipeline.</summary>
    [Parameter(ParameterSetName = "ByValue", ValueFromPipeline = true)]
    public ApiSessionInfo? SessionInfo { get; set; }

    /// <summary>Name of a previously registered session definition.</summary>
    [Parameter(ParameterSetName = "ByName", Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string SessionName { get; set; } = string.Empty;

    /// <summary>Optional cancellation token for network operations.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    //─────────────────────── Cmdlet lifecycle ───────────────────

    /// <inheritdoc/>
    protected override void ProcessRecord()
    {
        // Resolve session info (throws terminating error if not found)
        var info = ResolveInfo();

        if (!ShouldProcess(info.Name, "Connect-ApiSession"))
            return;

        // Execute async work on the SAME thread (avoid marshalling issues)
        var session = ConnectAsync(info, CancellationToken).GetAwaiter().GetResult();

        SessionRegistry.Add(session);
        WriteObject(session); // safe – we’re still on pipeline thread
    }

    //──────────────────────── Helper methods ────────────────────

    private static async Task<ApiSession> ConnectAsync(ApiSessionInfo info, CancellationToken ct)
    {
        var session = new ApiSession(info);

        // Validate connectivity by hitting root endpoint
        await session.GetStringAsync("/", ct).ConfigureAwait(false);

        return session;
    }

    private ApiSessionInfo ResolveInfo()
    {
        if (SessionInfo is not null)
            return SessionInfo;

        if (SessionRegistry.TryGet(SessionName, out var live))
            return live.Info;

        ThrowTerminatingError(new ErrorRecord(
            new ItemNotFoundException($"Session '{SessionName}' not found."),
            "SessionInfoNotFound",
            ErrorCategory.ObjectNotFound,
            SessionName));

        throw new InvalidOperationException("Unreachable code");
    }
}
