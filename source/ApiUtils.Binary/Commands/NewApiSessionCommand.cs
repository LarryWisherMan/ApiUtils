using System;
using System.Management.Automation;
using System.Net.Http;
using ApiUtils.Models;
using ApiUtils.Services;
using System.Threading.Tasks;

namespace ApiUtils.Commands
{
    /// <summary>
    /// Creates a new API session object for REST API communication.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "ApiSession", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
    [OutputType(typeof(ApiSession))]
    public class NewApiSessionCommand : PSCmdlet
    {
        /// <summary>
        /// Gets or sets the name of the service for which the session is being created.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the base URI of the API service.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNull]
        public Uri BaseUri { get; set; }

        /// <summary>
        /// Gets or sets optional PSCredential object for authentication.
        /// </summary>
        [Parameter(Mandatory = false)]
        public PSCredential Credentials { get; set; }

        /// <summary>
        /// Gets or sets optional UserAgent string sent to the API service.
        /// </summary>
        [Parameter(Mandatory = false)]
        public string UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the authentication type to use.
        /// </summary>
        [Parameter(Mandatory = false)]
        public AuthenticationType AuthType { get; set; } = AuthenticationType.None;

        /// <summary>
        /// Gets or sets the session timeout in minutes.
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateRange(1, 1440)] // 1 minute to 24 hours
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets whether to add the created session to the global session store.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AddToSessionStore { get; set; }

        /// <summary>
        /// Gets or sets additional headers to include in requests.
        /// </summary>
        [Parameter(Mandatory = false)]
        public System.Collections.Hashtable Headers { get; set; }

        /// <summary>
        /// Processes the record
        /// </summary>
        protected override void ProcessRecord()
        {
            if (!ShouldProcess($"Creating session for service: {ServiceName}", "New-ApiSession", $"Create API session for {ServiceName}"))
            {
                WriteVerbose("Session creation skipped due to -WhatIf or -Confirm.");
                return;
            }

            try
            {
                WriteVerbose("Initializing New-ApiSession function...");

                // Check if session already exists when adding to store
                if (AddToSessionStore.IsPresent)
                {
                    var existingSessionTask = SessionManager.GetSession(ServiceName);
                    existingSessionTask.Wait();
                    var existingSession = existingSessionTask.Result;

                    if (existingSession != null)
                    {
                        var exception = new InvalidOperationException($"A session with the name '{ServiceName}' already exists.");
                        var errorRecord = new ErrorRecord(
                            exception,
                            "SessionAlreadyExists",
                            ErrorCategory.ResourceExists,
                            ServiceName);
                        ThrowTerminatingError(errorRecord);
                    }
                }

                // Create the ApiSession object
                var session = new ApiSession(ServiceName, BaseUri, AuthType)
                {
                    Credential = Credentials,
                    TimeoutMinutes = TimeoutMinutes
                };

                // Set up HttpClient
                session.HttpClient = new HttpClient();
                session.HttpClient.BaseAddress = BaseUri;
                session.HttpClient.Timeout = TimeSpan.FromMinutes(TimeoutMinutes);

                // Set User-Agent if provided
                if (!string.IsNullOrWhiteSpace(UserAgent))
                {
                    session.HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                }

                // Add custom headers if provided
                if (Headers != null)
                {
                    foreach (System.Collections.DictionaryEntry header in Headers)
                    {
                        var key = header.Key.ToString();
                        var value = header.Value?.ToString();

                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                        {
                            session.Headers[key] = value;

                            try
                            {
                                session.HttpClient.DefaultRequestHeaders.Add(key, value);
                            }
                            catch (InvalidOperationException)
                            {
                                // Some headers can't be added to DefaultRequestHeaders
                                WriteVerbose($"Header '{key}' will be added per-request");
                            }
                        }
                    }
                }

                // Apply credentials if provided
                if (Credentials != null)
                {
                    WriteVerbose("Applying provided credentials to session.");
                    // Set AuthType to Basic if not explicitly set and credentials provided
                    if (session.AuthType == AuthenticationType.None)
                    {
                        session.AuthType = AuthenticationType.Basic;
                    }
                }

                // Add to session store if requested
                if (AddToSessionStore.IsPresent)
                {
                    var addTask = SessionManager.AddSession(session);
                    addTask.Wait();

                    if (addTask.Result)
                    {
                        WriteVerbose($"Session added to SessionStore for service: {ServiceName}");
                    }
                    else
                    {
                        WriteWarning($"Failed to add session '{ServiceName}' to session store");
                    }
                }

                WriteVerbose($"New session created for service: {ServiceName} with base URI: {BaseUri}");
                WriteObject(session);
            }
            catch (Exception ex)
            {
                var errorRecord = new ErrorRecord(
                    ex,
                    "NewApiSessionFailed",
                    ErrorCategory.InvalidOperation,
                    ServiceName);
                WriteError(errorRecord);
            }
        }

        /// <summary>
        /// End processing
        /// </summary>
        protected override void EndProcessing()
        {
            WriteVerbose("New-ApiSession function execution completed.");
        }
    }
}
