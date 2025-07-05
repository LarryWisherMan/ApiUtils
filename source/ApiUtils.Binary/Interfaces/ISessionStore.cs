// File: ApiUtils/Interfaces/ISessionStore.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApiUtils.Models;

namespace ApiUtils.Interfaces
{
    /// <summary>
    /// Interface for session storage implementations
    /// </summary>
    public interface ISessionStore
    {
        /// <summary>
        /// Adds a session to the store
        /// </summary>
        /// <param name="session">The session to add</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> AddSessionAsync(ApiSession session);

        /// <summary>
        /// Gets a session by name
        /// </summary>
        /// <param name="name">The session name</param>
        /// <returns>The session if found, null otherwise</returns>
        Task<ApiSession> GetSessionAsync(string name);

        /// <summary>
        /// Removes a session from the store
        /// </summary>
        /// <param name="name">The session name</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> RemoveSessionAsync(string name);

        /// <summary>
        /// Gets all sessions
        /// </summary>
        /// <returns>Collection of all sessions</returns>
        Task<IEnumerable<ApiSession>> GetAllSessionsAsync();

        /// <summary>
        /// Clears all sessions
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> ClearAllSessionsAsync();

        /// <summary>
        /// Checks if a session exists
        /// </summary>
        /// <param name="name">The session name</param>
        /// <returns>True if exists, false otherwise</returns>
        Task<bool> SessionExistsAsync(string name);

        /// <summary>
        /// Removes expired sessions
        /// </summary>
        /// <returns>Number of sessions removed</returns>
        Task<int> CleanupExpiredSessionsAsync();
    }
}

