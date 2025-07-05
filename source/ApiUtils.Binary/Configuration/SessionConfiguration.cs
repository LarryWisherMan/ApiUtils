// File: ApiUtils/Configuration/SessionConfiguration.cs
using System;
using System.Collections.Generic;
using ApiUtils.Models;

namespace ApiUtils.Configuration
{
    /// <summary>
    /// Configuration options for session management
    /// </summary>
    public class SessionConfiguration
    {
        /// <summary>
        /// Gets or sets the default session timeout in minutes
        /// </summary>
        public int DefaultTimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the cleanup interval in minutes
        /// </summary>
        public int CleanupIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets whether to auto-cleanup expired sessions
        /// </summary>
        public bool AutoCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of sessions to keep
        /// </summary>
        public int MaxSessions { get; set; } = 100;

        /// <summary>
        /// Gets or sets default headers to apply to all sessions
        /// </summary>
        public Dictionary<string, string> DefaultHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets the default authentication type
        /// </summary>
        public AuthenticationType DefaultAuthType { get; set; } = AuthenticationType.None;

        /// <summary>
        /// Gets or sets whether to validate SSL certificates
        /// </summary>
        public bool ValidateCertificates { get; set; } = true;

        /// <summary>
        /// Gets or sets the default user agent
        /// </summary>
        public string DefaultUserAgent { get; set; } = "ApiUtils/1.0";

        /// <summary>
        /// Gets or sets the session store type
        /// </summary>
        public SessionStoreType StoreType { get; set; } = SessionStoreType.Memory;

        /// <summary>
        /// Gets or sets the file path for file-based storage
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets whether to enable session events
        /// </summary>
        public bool EnableEvents { get; set; } = true;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public void Validate()
        {
            if (DefaultTimeoutMinutes < 1)
                throw new ArgumentException("DefaultTimeoutMinutes must be at least 1");

            if (CleanupIntervalMinutes < 1)
                throw new ArgumentException("CleanupIntervalMinutes must be at least 1");

            if (MaxSessions < 1)
                throw new ArgumentException("MaxSessions must be at least 1");

            if (StoreType == SessionStoreType.File && string.IsNullOrWhiteSpace(FilePath))
                throw new ArgumentException("FilePath must be specified for file-based storage");
        }
    }

    /// <summary>
    /// Session store types
    /// </summary>
    public enum SessionStoreType
    {
        Memory,
        File,
        PowerShellVariable
    }
}
