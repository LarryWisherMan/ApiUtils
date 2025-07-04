using System;
using System.Collections.Generic;

namespace ApiUtils.Models
{
    /// <summary>
    /// Represents an API session with authentication and configuration details
    /// </summary>
    public class ApiSession
    {
        /// <summary>
        /// Gets or sets the session name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the base URI for the API
        /// </summary>
        public Uri BaseUri { get; set; }

        /// <summary>
        /// Gets or sets the authentication type
        /// </summary>
        public AuthenticationType AuthType { get; set; }

        /// <summary>
        /// Gets or sets whether the session is authenticated
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Gets or sets when the session was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the session was last authenticated
        /// </summary>
        public DateTime? LastAuthenticated { get; set; }

        /// <summary>
        /// Gets or sets the session timeout in minutes
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets additional headers for requests
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Initializes a new instance of the ApiSession class
        /// </summary>
        public ApiSession()
        {
            Headers = new Dictionary<string, string>();
            CreatedAt = DateTime.UtcNow;
            AuthType = AuthenticationType.None;
        }

        /// <summary>
        /// Checks if the session has expired
        /// </summary>
        /// <returns>True if the session has expired, false otherwise</returns>
        public bool IsExpired()
        {
            if (!LastAuthenticated.HasValue)
                return !IsAuthenticated;

            return DateTime.UtcNow.Subtract(LastAuthenticated.Value).TotalMinutes > TimeoutMinutes;
        }

        /// <summary>
        /// Returns a string representation of the session
        /// </summary>
        public override string ToString()
        {
            var status = IsAuthenticated ? (IsExpired() ? "Expired" : "Active") : "Not Authenticated";
            return $"{Name} ({BaseUri}) - {AuthType} [{status}]";
        }
    }
}
