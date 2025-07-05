// File: ApiUtils / Services / PowerShellVariableSessionStore.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using ApiUtils.Interfaces;
using ApiUtils.Models;

namespace ApiUtils.Services
{
    /// <summary>
    /// PowerShell variable-based session store implementation
    /// </summary>
    public class PowerShellVariableSessionStore : ISessionStore
    {
        private readonly SessionState _sessionState;
        private readonly string _variableName;

        public PowerShellVariableSessionStore(SessionState sessionState, string variableName = "ApiSessions")
        {
            _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
            _variableName = variableName;
        }

        public Task<bool> AddSessionAsync(ApiSession session)
        {
            if (session == null) return Task.FromResult(false);

            var sessions = GetSessionDictionary();

            if (sessions.ContainsKey(session.Name))
                return Task.FromResult(false);

            sessions[session.Name] = session;
            SetSessionDictionary(sessions);

            return Task.FromResult(true);
        }

        public Task<ApiSession> GetSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Task.FromResult<ApiSession>(null);

            var sessions = GetSessionDictionary();
            sessions.TryGetValue(name, out var session);

            // Check if expired
            if (session?.IsExpired == true || session?.IsDisposed == true)
            {
                sessions.Remove(name);
                SetSessionDictionary(sessions);
                session?.Dispose();
                return Task.FromResult<ApiSession>(null);
            }

            return Task.FromResult(session);
        }

        public Task<bool> RemoveSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(false);

            var sessions = GetSessionDictionary();

            if (sessions.TryGetValue(name, out var session))
            {
                sessions.Remove(name);
                SetSessionDictionary(sessions);
                session?.Dispose();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<IEnumerable<ApiSession>> GetAllSessionsAsync()
        {
            var sessions = GetSessionDictionary();
            var validSessions = sessions.Values
                .Where(s => s != null && !s.IsExpired && !s.IsDisposed)
                .ToList();

            return Task.FromResult<IEnumerable<ApiSession>>(validSessions);
        }

        public Task<bool> ClearAllSessionsAsync()
        {
            var sessions = GetSessionDictionary();

            foreach (var session in sessions.Values)
            {
                session?.Dispose();
            }

            SetSessionDictionary(new Dictionary<string, ApiSession>());
            return Task.FromResult(true);
        }

        public Task<bool> SessionExistsAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(false);

            var sessions = GetSessionDictionary();
            return Task.FromResult(sessions.ContainsKey(name) &&
                                   sessions[name]?.IsExpired == false &&
                                   sessions[name]?.IsDisposed == false);
        }

        public Task<int> CleanupExpiredSessionsAsync()
        {
            var sessions = GetSessionDictionary();
            var expiredSessions = sessions
                .Where(kvp => kvp.Value?.IsExpired == true || kvp.Value?.IsDisposed == true)
                .ToList();

            int removedCount = 0;
            foreach (var expired in expiredSessions)
            {
                sessions.Remove(expired.Key);
                expired.Value?.Dispose();
                removedCount++;
            }

            if (removedCount > 0)
            {
                SetSessionDictionary(sessions);
            }

            return Task.FromResult(removedCount);
        }

        private Dictionary<string, ApiSession> GetSessionDictionary()
        {
            try
            {
                var variable = _sessionState.PSVariable.Get(_variableName);
                return variable?.Value as Dictionary<string, ApiSession> ?? new Dictionary<string, ApiSession>();
            }
            catch
            {
                return new Dictionary<string, ApiSession>();
            }
        }

        private void SetSessionDictionary(Dictionary<string, ApiSession> sessions)
        {
            try
            {
                _sessionState.PSVariable.Set(_variableName, sessions);
            }
            catch
            {
                // Ignore errors setting PowerShell variable
            }
        }
    }
}
