// File: ApiUtils/Commands/SetApiSessionStoreCommand.cs
using System;
using System.Linq;
using System.Management.Automation;
using ApiUtils.Configuration;
using ApiUtils.Services;

namespace ApiUtils.Commands
{
    /// <summary>
    /// Configures the session store for API sessions
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "ApiSessionStore", SupportsShouldProcess = true)]
    public class SetApiSessionStoreCommand : PSCmdlet
    {
        /// <summary>
        /// Gets or sets the session store type
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public SessionStoreType StoreType { get; set; }

        /// <summary>
        /// Gets or sets the file path for file-based storage
        /// </summary>
        [Parameter(Mandatory = false)]
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the PowerShell variable name for variable-based storage
        /// </summary>
        [Parameter(Mandatory = false)]
        public string VariableName { get; set; } = "ApiSessions";

        /// <summary>
        /// Gets or sets whether to migrate existing sessions
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter MigrateExistingSessions { get; set; }

        protected override void ProcessRecord()
        {
            if (!ShouldProcess($"Setting session store to {StoreType}", "Set-ApiSessionStore", $"Change session store type to {StoreType}"))
            {
                return;
            }

            try
            {
                var config = new SessionConfiguration
                {
                    StoreType = StoreType,
                    FilePath = FilePath
                };

                var newStore = SessionStoreFactory.CreateStore(config, SessionState);

                if (MigrateExistingSessions.IsPresent)
                {
                    // Get existing sessions before changing store
                    var existingSessionsTask = SessionManager.GetAllSessions();
                    existingSessionsTask.Wait();
                    var existingSessions = existingSessionsTask.Result;

                    // Set new store
                    SessionManager.SetSessionStore(newStore);

                    // Migrate sessions
                    foreach (var session in existingSessions)
                    {
                        var addTask = newStore.AddSessionAsync(session);
                        addTask.Wait();
                    }

                    WriteVerbose($"Migrated {existingSessions.Count()} sessions to new store");
                }
                else
                {
                    SessionManager.SetSessionStore(newStore);
                }

                WriteVerbose($"Session store changed to {StoreType}");

                if (StoreType == SessionStoreType.File && !string.IsNullOrWhiteSpace(FilePath))
                {
                    WriteVerbose($"File path: {FilePath}");
                }
                else if (StoreType == SessionStoreType.PowerShellVariable)
                {
                    WriteVerbose($"Variable name: {VariableName}");
                }
            }
            catch (Exception ex)
            {
                var errorRecord = new ErrorRecord(
                    ex,
                    "SetSessionStoreFailed",
                    ErrorCategory.InvalidOperation,
                    StoreType);
                WriteError(errorRecord);
            }
        }
    }
}
