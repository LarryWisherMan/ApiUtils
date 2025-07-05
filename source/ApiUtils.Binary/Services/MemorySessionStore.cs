// File: ApiUtils/Services/MemorySessionStore.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiUtils.Interfaces;
using ApiUtils.Models;

namespace ApiUtils.Services
{
    /// <summary>
    /// In-memory session store implementation
    /// </summary>
    public class MemorySessionStore : ISessionStore
    {
        private readonly ConcurrentDictionary<string, ApiSession> _sessions = new();

        public Task<bool> AddSessionAsync(ApiSession session)
        {
            if (session == null) return Task.FromResult(false);

            return Task.FromResult(_sessions.TryAdd(session.Name, session));
        }

        public Task<ApiSession> GetSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Task.FromResult<ApiSession>(null);

            _sessions.TryGetValue(name, out var session);

            // Return null if session is expired or disposed
            if (session?.IsExpired == true || session?.IsDisposed == true)
            {
                _ = Task.Run(() => RemoveSessionAsync(name));
                return Task.FromResult<ApiSession>(null);
            }

            return Task.FromResult(session);
        }

        public Task<bool> RemoveSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(false);

            if (_sessions.TryRemove(name, out var session))
            {
                session?.Dispose();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<IEnumerable<ApiSession>> GetAllSessionsAsync()
        {
            var validSessions = _sessions.Values
                .Where(s => s != null && !s.IsExpired && !s.IsDisposed)
                .ToList();

            return Task.FromResult<IEnumerable<ApiSession>>(validSessions);
        }

        public Task<bool> ClearAllSessionsAsync()
        {
            var sessions = _sessions.Values.ToList();
            _sessions.Clear();

            // Dispose all sessions
            foreach (var session in sessions)
            {
                session?.Dispose();
            }

            return Task.FromResult(true);
        }

        public Task<bool> SessionExistsAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(false);

            return Task.FromResult(_sessions.ContainsKey(name) &&
                                   _sessions[name]?.IsExpired == false &&
                                   _sessions[name]?.IsDisposed == false);
        }

        public Task<int> CleanupExpiredSessionsAsync()
        {
            var expiredSessions = _sessions
                .Where(kvp => kvp.Value?.IsExpired == true || kvp.Value?.IsDisposed == true)
                .ToList();

            int removedCount = 0;
            foreach (var expired in expiredSessions)
            {
                if (_sessions.TryRemove(expired.Key, out var session))
                {
                    session?.Dispose();
                    removedCount++;
                }
            }

            return Task.FromResult(removedCount);
        }
    }
}

