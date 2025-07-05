// File: ApiUtils/Interfaces/ISessionManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApiUtils.Models;

namespace ApiUtils.Interfaces
{
    /// <summary>
    /// Interface for session management
    /// </summary>
    public interface ISessionManager : IDisposable
    {
        /// <summary>
        /// Event raised when a session is added
        /// </summary>
        event EventHandler<SessionEventArgs> SessionAdded;

        /// <summary>
        /// Event raised when a session is removed
        /// </summary>
        event EventHandler<SessionEventArgs> SessionRemoved;

        /// <summary>
        /// Event raised when a session expires
        /// </summary>
        event EventHandler<SessionEventArgs> SessionExpired;

        /// <summary>
        /// Adds a session to the manager
        /// </summary>
        Task<bool> AddSessionAsync(ApiSession session);

        /// <summary>
        /// Gets a session by name
        /// </summary>
        Task<ApiSession> GetSessionAsync(string name);

        /// <summary>
        /// Removes a session
        /// </summary>
        Task<bool> RemoveSessionAsync(string name);

        /// <summary>
        /// Gets all active sessions
        /// </summary>
        Task<IEnumerable<ApiSession>> GetAllSessionsAsync();

        /// <summary>
        /// Clears all sessions
        /// </summary>
        Task<bool> ClearAllSessionsAsync();

        /// <summary>
        /// Checks if a session exists and is valid
        /// </summary>
        Task<bool> SessionExistsAsync(string name);

        /// <summary>
        /// Gets session statistics
        /// </summary>
        Task<SessionStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// Event arguments for session events
    /// </summary>
    public class SessionEventArgs : EventArgs
    {
        public ApiSession Session { get; }
        public string Reason { get; }

        public SessionEventArgs(ApiSession session, string reason = null)
        {
            Session = session;
            Reason = reason;
        }
    }

    /// <summary>
    /// Statistics about sessions
    /// </summary>
    public class SessionStatistics
    {
        public int TotalSessions { get; set; }
        public int ActiveSessions { get; set; }
        public int ExpiredSessions { get; set; }
        public int AuthenticatedSessions { get; set; }
        public DateTime? OldestSession { get; set; }
        public DateTime? NewestSession { get; set; }
        public Dictionary<AuthenticationType, int> SessionsByAuthType { get; set; } = new();
    }
}
