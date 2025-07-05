// File: ApiUtils/Services/HybridSessionStore.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiUtils.Interfaces;
using ApiUtils.Models;

namespace ApiUtils.Services
{
    /// <summary>
    /// Hybrid session store that uses multiple backends
    /// </summary>
    public class HybridSessionStore : ISessionStore
    {
        private readonly ISessionStore _primaryStore;
        private readonly ISessionStore _backupStore;

        public HybridSessionStore(ISessionStore primaryStore, ISessionStore backupStore)
        {
            _primaryStore = primaryStore ?? throw new ArgumentNullException(nameof(primaryStore));
            _backupStore = backupStore ?? throw new ArgumentNullException(nameof(backupStore));
        }

        public async Task<bool> AddSessionAsync(ApiSession session)
        {
            var primaryResult = await _primaryStore.AddSessionAsync(session);

            if (primaryResult && _backupStore != null)
            {
                try
                {
                    await _backupStore.AddSessionAsync(session);
                }
                catch
                {
                    // Backup store failure shouldn't fail the operation
                }
            }

            return primaryResult;
        }

        public async Task<ApiSession> GetSessionAsync(string name)
        {
            var session = await _primaryStore.GetSessionAsync(name);

            if (session == null && _backupStore != null)
            {
                try
                {
                    session = await _backupStore.GetSessionAsync(name);
                    if (session != null)
                    {
                        // Restore to primary store
                        await _primaryStore.AddSessionAsync(session);
                    }
                }
                catch
                {
                    // Backup store failure shouldn't fail the operation
                }
            }

            return session;
        }

        public async Task<bool> RemoveSessionAsync(string name)
        {
            var primaryResult = await _primaryStore.RemoveSessionAsync(name);

            if (_backupStore != null)
            {
                try
                {
                    await _backupStore.RemoveSessionAsync(name);
                }
                catch
                {
                    // Backup store failure shouldn't fail the operation
                }
            }

            return primaryResult;
        }

        public async Task<IEnumerable<ApiSession>> GetAllSessionsAsync()
        {
            var primarySessions = await _primaryStore.GetAllSessionsAsync();

            if (_backupStore != null)
            {
                try
                {
                    var backupSessions = await _backupStore.GetAllSessionsAsync();
                    var allSessions = primarySessions
                        .Concat(backupSessions)
                        .GroupBy(s => s.Name)
                        .Select(g => g.First()) // Take first occurrence (prefer primary)
                        .ToList();

                    return allSessions;
                }
                catch
                {
                    // Fall back to primary only
                }
            }

            return primarySessions;
        }

        public async Task<bool> ClearAllSessionsAsync()
        {
            var primaryResult = await _primaryStore.ClearAllSessionsAsync();

            if (_backupStore != null)
            {
                try
                {
                    await _backupStore.ClearAllSessionsAsync();
                }
                catch
                {
                    // Backup store failure shouldn't fail the operation
                }
            }

            return primaryResult;
        }

        public async Task<bool> SessionExistsAsync(string name)
        {
            var existsInPrimary = await _primaryStore.SessionExistsAsync(name);

            if (!existsInPrimary && _backupStore != null)
            {
                try
                {
                    return await _backupStore.SessionExistsAsync(name);
                }
                catch
                {
                    // Backup store failure shouldn't fail the operation
                }
            }

            return existsInPrimary;
        }

        public async Task<int> CleanupExpiredSessionsAsync()
        {
            var primaryCleanup = await _primaryStore.CleanupExpiredSessionsAsync();
            var backupCleanup = 0;

            if (_backupStore != null)
            {
                try
                {
                    backupCleanup = await _backupStore.CleanupExpiredSessionsAsync();
                }
                catch
                {
                    // Backup store failure shouldn't fail the operation
                }
            }

            return primaryCleanup + backupCleanup;
        }
    }
}
