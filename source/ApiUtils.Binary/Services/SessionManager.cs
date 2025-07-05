// File: ApiUtils/Services/SessionManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Interfaces;
using ApiUtils.Models;

namespace ApiUtils.Services
{
    /// <summary>
    /// Manages API sessions with configurable storage backend
    /// </summary>
    public class SessionManager : ISessionManager
    {
        private static readonly Lazy<SessionManager> _instance = new(() => new SessionManager());
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _operationLock = new(1, 1);
        private ISessionStore _store;
        private bool _disposed = false;

        /// <summary>
        /// Gets the singleton instance of SessionManager
        /// </summary>
        public static SessionManager Instance => _instance.Value;

        public event EventHandler<SessionEventArgs> SessionAdded;
        public event EventHandler<SessionEventArgs> SessionRemoved;
        public event EventHandler<SessionEventArgs> SessionExpired;

        /// <summary>
        /// Initializes a new instance of SessionManager
        /// </summary>
        private SessionManager()
        {
            _store = new MemorySessionStore();

            // Set up cleanup timer (every 5 minutes)
            _cleanupTimer = new Timer(CleanupCallback, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Sets the storage backend for sessions
        /// </summary>
        /// <param name="store">The session store to use</param>
        public void SetStore(ISessionStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            _store = store;
        }

        /// <summary>
        /// Adds a session to the manager
        /// </summary>
        public async Task<bool> AddSessionAsync(ApiSession session)
        {
            if (session == null) return false;
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            await _operationLock.WaitAsync();
            try
            {
                var result = await _store.AddSessionAsync(session);

                if (result)
                {
                    SessionAdded?.Invoke(this, new SessionEventArgs(session, "Added to store"));
                }

                return result;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Gets a session by name
        /// </summary>
        public async Task<ApiSession> GetSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            var session = await _store.GetSessionAsync(name);

            // Check if session is expired
            if (session?.IsExpired == true)
            {
                SessionExpired?.Invoke(this, new SessionEventArgs(session, "Session expired"));
                _ = Task.Run(() => RemoveSessionAsync(name));
                return null;
            }

            return session;
        }

        /// <summary>
        /// Removes a session
        /// </summary>
        public async Task<bool> RemoveSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            await _operationLock.WaitAsync();
            try
            {
                var session = await _store.GetSessionAsync(name);
                var result = await _store.RemoveSessionAsync(name);

                if (result && session != null)
                {
                    SessionRemoved?.Invoke(this, new SessionEventArgs(session, "Removed from store"));
                }

                return result;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Gets all active sessions
        /// </summary>
        public async Task<IEnumerable<ApiSession>> GetAllSessionsAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            return await _store.GetAllSessionsAsync();
        }

        /// <summary>
        /// Clears all sessions
        /// </summary>
        public async Task<bool> ClearAllSessionsAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            await _operationLock.WaitAsync();
            try
            {
                var sessions = await _store.GetAllSessionsAsync();
                var result = await _store.ClearAllSessionsAsync();

                if (result)
                {
                    foreach (var session in sessions)
                    {
                        SessionRemoved?.Invoke(this, new SessionEventArgs(session, "Cleared from store"));
                    }
                }

                return result;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Checks if a session exists and is valid
        /// </summary>
        public async Task<bool> SessionExistsAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            var session = await GetSessionAsync(name);
            return session?.IsValid() == true;
        }

        /// <summary>
        /// Gets session statistics
        /// </summary>
        public async Task<SessionStatistics> GetStatisticsAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            var sessions = await _store.GetAllSessionsAsync();
            var sessionList = sessions.ToList();

            var stats = new SessionStatistics
            {
                TotalSessions = sessionList.Count,
                ActiveSessions = sessionList.Count(s => !s.IsExpired && s.IsAuthenticated),
                ExpiredSessions = sessionList.Count(s => s.IsExpired),
                AuthenticatedSessions = sessionList.Count(s => s.IsAuthenticated),
                OldestSession = sessionList.Any() ? sessionList.Min(s => s.CreatedAt) : null,
                NewestSession = sessionList.Any() ? sessionList.Max(s => s.CreatedAt) : null
            };

            // Group by auth type
            foreach (var group in sessionList.GroupBy(s => s.AuthType))
            {
                stats.SessionsByAuthType[group.Key] = group.Count();
            }

            return stats;
        }

        /// <summary>
        /// Manually triggers cleanup of expired sessions
        /// </summary>
        public async Task<int> CleanupExpiredSessionsAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));

            var removedCount = await _store.CleanupExpiredSessionsAsync();

            if (removedCount > 0)
            {
                // Note: We can't get the actual sessions that were removed for events
                // since they're already cleaned up
            }

            return removedCount;
        }

        /// <summary>
        /// Timer callback for automatic cleanup
        /// </summary>
        private async void CleanupCallback(object state)
        {
            if (_disposed) return;

            try
            {
                await CleanupExpiredSessionsAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Static Helper Methods for Backward Compatibility

        /// <summary>
        /// Adds a session using the singleton instance
        /// </summary>
        public static Task<bool> AddSession(ApiSession session)
        {
            return Instance.AddSessionAsync(session);
        }

        /// <summary>
        /// Gets a session using the singleton instance
        /// </summary>
        public static Task<ApiSession> GetSession(string name)
        {
            return Instance.GetSessionAsync(name);
        }

        /// <summary>
        /// Removes a session using the singleton instance
        /// </summary>
        public static Task<bool> RemoveSession(string name)
        {
            return Instance.RemoveSessionAsync(name);
        }

        /// <summary>
        /// Checks if a session exists using the singleton instance
        /// </summary>
        public static Task<bool> SessionExists(string name)
        {
            return Instance.SessionExistsAsync(name);
        }

        /// <summary>
        /// Gets all sessions using the singleton instance
        /// </summary>
        public static Task<IEnumerable<ApiSession>> GetAllSessions()
        {
            return Instance.GetAllSessionsAsync();
        }

        /// <summary>
        /// Sets the store for the singleton instance
        /// </summary>
        public static void SetSessionStore(ISessionStore store)
        {
            Instance.SetStore(store);
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cleanupTimer?.Dispose();
                _operationLock?.Dispose();

                // Don't dispose the store as it might be shared
                _disposed = true;
            }
        }

        ~SessionManager()
        {
            Dispose(false);
        }

        #endregion
    }
}
