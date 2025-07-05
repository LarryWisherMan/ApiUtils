using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Management.Automation;
using System.Threading;

namespace ApiUtils.Models
{
    /// <summary>
    /// Represents an API session with authentication, configuration details, and HTTP client management
    /// </summary>
    public class ApiSession : IDisposable
    {
        private readonly Timer _expirationTimer;
        private bool _disposed = false;
        private HttpClient _httpClient;

        /// <summary>
        /// Gets the session name (immutable after creation)
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the base URI for the API (immutable after creation)
        /// </summary>
        public Uri BaseUri { get; }

        /// <summary>
        /// Gets or sets the authentication type
        /// </summary>
        public AuthenticationType AuthType { get; set; }

        /// <summary>
        /// Gets or sets whether the session is authenticated
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Gets when the session was created
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets or sets when the session was last authenticated
        /// </summary>
        public DateTime? LastAuthenticated { get; set; }

        /// <summary>
        /// Gets when the session expires
        /// </summary>
        public DateTime ExpiresAt { get; private set; }

        /// <summary>
        /// Gets or sets the session timeout in minutes
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the PowerShell credential object
        /// </summary>
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Gets additional headers for requests
        /// </summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets the HTTP client for this session
        /// </summary>
        public HttpClient HttpClient
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ApiSession));
                return _httpClient;
            }
            set
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ApiSession));

                _httpClient?.Dispose();
                _httpClient = value;
            }
        }

        /// <summary>
        /// Gets a unique identifier for this session
        /// </summary>
        public string Id { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>
        /// Gets whether the session has expired
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        /// <summary>
        /// Gets whether the session is disposed
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Initializes a new instance of the ApiSession class
        /// </summary>
        /// <param name="name">The session name</param>
        /// <param name="baseUri">The base URI for the API</param>
        /// <param name="authType">The authentication type</param>
        public ApiSession(string name, Uri baseUri, AuthenticationType authType = AuthenticationType.None)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Session name cannot be null or empty", nameof(name));

            Name = name;
            BaseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
            AuthType = authType;
            CreatedAt = DateTime.UtcNow;

            RefreshExpiration();

            // Set up auto-refresh timer (every 5 minutes)
            _expirationTimer = new Timer(AutoRefreshCallback, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Refreshes the session expiration time
        /// </summary>
        public void RefreshExpiration()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ApiSession));

            ExpiresAt = DateTime.UtcNow.AddMinutes(TimeoutMinutes);
        }

        /// <summary>
        /// Marks the session as authenticated
        /// </summary>
        public void MarkAsAuthenticated()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ApiSession));

            IsAuthenticated = true;
            LastAuthenticated = DateTime.UtcNow;
            RefreshExpiration();
        }

        /// <summary>
        /// Marks the session as not authenticated
        /// </summary>
        public void MarkAsNotAuthenticated()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ApiSession));

            IsAuthenticated = false;
            LastAuthenticated = null;
        }

        /// <summary>
        /// Validates that the session is in a usable state
        /// </summary>
        /// <returns>True if the session is valid and not expired</returns>
        public bool IsValid()
        {
            return !_disposed && !IsExpired && HttpClient != null;
        }

        /// <summary>
        /// Creates a copy of the session with updated timeout
        /// </summary>
        /// <param name="newTimeoutMinutes">The new timeout in minutes</param>
        /// <returns>A new ApiSession with updated timeout</returns>
        public ApiSession WithTimeout(int newTimeoutMinutes)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ApiSession));

            var newSession = new ApiSession(Name, BaseUri, AuthType)
            {
                TimeoutMinutes = newTimeoutMinutes,
                IsAuthenticated = IsAuthenticated,
                LastAuthenticated = LastAuthenticated,
                Credential = Credential
            };

            // Copy headers
            foreach (var header in Headers)
            {
                newSession.Headers[header.Key] = header.Value;
            }

            return newSession;
        }

        /// <summary>
        /// Auto-refresh callback for the timer
        /// </summary>
        private void AutoRefreshCallback(object state)
        {
            if (!_disposed && IsAuthenticated && !IsExpired)
            {
                RefreshExpiration();
            }
        }

        /// <summary>
        /// Returns a string representation of the session
        /// </summary>
        public override string ToString()
        {
            if (_disposed)
                return $"{Name} - Disposed";

            var status = IsAuthenticated ? (IsExpired ? "Expired" : "Active") : "Not Authenticated";
            return $"{Name} ({BaseUri}) - {AuthType} [{status}] (ID: {Id})";
        }

        /// <summary>
        /// Returns detailed information about the session
        /// </summary>
        public string GetDetailedInfo()
        {
            if (_disposed)
                return $"Session '{Name}' is disposed";

            var info = new List<string>
            {
                $"Name: {Name}",
                $"ID: {Id}",
                $"Base URI: {BaseUri}",
                $"Auth Type: {AuthType}",
                $"Authenticated: {IsAuthenticated}",
                $"Created: {CreatedAt:yyyy-MM-dd HH:mm:ss} UTC",
                $"Expires: {ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC",
                $"Timeout: {TimeoutMinutes} minutes",
                $"Headers: {Headers.Count}",
                $"Valid: {IsValid()}"
            };

            if (LastAuthenticated.HasValue)
                info.Add($"Last Authenticated: {LastAuthenticated:yyyy-MM-dd HH:mm:ss} UTC");

            return string.Join(Environment.NewLine, info);
        }

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the ApiSession
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _expirationTimer?.Dispose();
                _httpClient?.Dispose();
                Headers?.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ApiSession()
        {
            Dispose(false);
        }

        #endregion
    }
}
