// File: ApiUtils/Services/SessionStoreFactory.cs
using System;
using System.Management.Automation;
using ApiUtils.Configuration;
using ApiUtils.Interfaces;

namespace ApiUtils.Services
{
    /// <summary>
    /// Factory for creating session store instances
    /// </summary>
    public static class SessionStoreFactory
    {
        /// <summary>
        /// Creates a session store based on configuration
        /// </summary>
        /// <param name="config">Session configuration</param>
        /// <param name="sessionState">PowerShell session state (for PowerShell variable store)</param>
        /// <returns>Session store instance</returns>
        public static ISessionStore CreateStore(SessionConfiguration config, SessionState sessionState = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            config.Validate();

            return config.StoreType switch
            {
                SessionStoreType.Memory => new MemorySessionStore(),
                SessionStoreType.File => new FileSessionStore(config.FilePath),
                SessionStoreType.PowerShellVariable => CreatePowerShellVariableStore(sessionState),
                _ => throw new ArgumentException($"Unsupported store type: {config.StoreType}")
            };
        }

        /// <summary>
        /// Creates a memory-based session store
        /// </summary>
        public static ISessionStore CreateMemoryStore()
        {
            return new MemorySessionStore();
        }

        /// <summary>
        /// Creates a file-based session store
        /// </summary>
        /// <param name="filePath">Path to the session file</param>
        public static ISessionStore CreateFileStore(string filePath = null)
        {
            return new FileSessionStore(filePath);
        }

        /// <summary>
        /// Creates a PowerShell variable-based session store
        /// </summary>
        /// <param name="sessionState">PowerShell session state</param>
        /// <param name="variableName">Variable name to store sessions</param>
        public static ISessionStore CreatePowerShellVariableStore(SessionState sessionState, string variableName = "ApiSessions")
        {
            if (sessionState == null)
                throw new ArgumentException("SessionState is required for PowerShell variable store");

            return new PowerShellVariableSessionStore(sessionState, variableName);
        }

        /// <summary>
        /// Creates a hybrid store that uses multiple backends
        /// </summary>
        /// <param name="primaryStore">Primary store for active operations</param>
        /// <param name="backupStore">Backup store for persistence</param>
        public static ISessionStore CreateHybridStore(ISessionStore primaryStore, ISessionStore backupStore)
        {
            return new HybridSessionStore(primaryStore, backupStore);
        }
    }
}
