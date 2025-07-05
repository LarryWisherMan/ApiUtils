// File: ApiUtils/Extensions/SessionManagerExtensions.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiUtils.Interfaces;
using ApiUtils.Models;
using ApiUtils.Services;

namespace ApiUtils.Extensions
{
    /// <summary>
    /// Extension methods for SessionManager
    /// </summary>
    public static class SessionManagerExtensions
    {
        /// <summary>
        /// Gets sessions by authentication type
        /// </summary>
        public static async Task<IEnumerable<ApiSession>> GetSessionsByAuthTypeAsync(
            this ISessionManager manager, AuthenticationType authType)
        {
            var sessions = await manager.GetAllSessionsAsync();
            return sessions.Where(s => s.AuthType == authType);
        }

        /// <summary>
        /// Gets active sessions (authenticated and not expired)
        /// </summary>
        public static async Task<IEnumerable<ApiSession>> GetActiveSessionsAsync(
            this ISessionManager manager)
        {
            var sessions = await manager.GetAllSessionsAsync();
            return sessions.Where(s => s.IsAuthenticated && !s.IsExpired);
        }

        /// <summary>
        /// Gets sessions that will expire within the specified timespan
        /// </summary>
        public static async Task<IEnumerable<ApiSession>> GetExpiringSessionsAsync(
            this ISessionManager manager, TimeSpan within)
        {
            var sessions = await manager.GetAllSessionsAsync();
            var expirationThreshold = DateTime.UtcNow.Add(within);

            return sessions.Where(s => !s.IsExpired && s.ExpiresAt <= expirationThreshold);
        }

        /// <summary>
        /// Refreshes expiration for all active sessions
        /// </summary>
        public static async Task RefreshAllSessionsAsync(this ISessionManager manager)
        {
            var sessions = await manager.GetActiveSessionsAsync();
            foreach (var session in sessions)
            {
                session.RefreshExpiration();
            }
        }

        /// <summary>
        /// Gets sessions by base URI pattern
        /// </summary>
        public static async Task<IEnumerable<ApiSession>> GetSessionsByUriPatternAsync(
            this ISessionManager manager, string pattern)
        {
            var sessions = await manager.GetAllSessionsAsync();
#if NET6_0_OR_GREATER
            return sessions.Where(s => s.BaseUri.ToString().Contains(pattern, StringComparison.OrdinalIgnoreCase));
#else
            return sessions.Where(s => s.BaseUri.ToString().IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
#endif
        }

        /// <summary>
        /// Bulk removes sessions by predicate
        /// </summary>
        public static async Task<int> RemoveSessionsAsync(
            this ISessionManager manager, Func<ApiSession, bool> predicate)
        {
            var sessions = await manager.GetAllSessionsAsync();
            var sessionsToRemove = sessions.Where(predicate).ToList();

            int removedCount = 0;
            foreach (var session in sessionsToRemove)
            {
                if (await manager.RemoveSessionAsync(session.Name))
                {
                    removedCount++;
                }
            }

            return removedCount;
        }

        /// <summary>
        /// Creates a session report
        /// </summary>
        public static async Task<string> GenerateSessionReportAsync(this ISessionManager manager)
        {
            var stats = await manager.GetStatisticsAsync();
            var sessions = await manager.GetAllSessionsAsync();

            var report = new List<string>
            {
                "=== API Session Report ===",
                $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                "",
                "Summary:",
                $"  Total Sessions: {stats.TotalSessions}",
                $"  Active Sessions: {stats.ActiveSessions}",
                $"  Expired Sessions: {stats.ExpiredSessions}",
                $"  Authenticated Sessions: {stats.AuthenticatedSessions}",
                ""
            };

            if (stats.OldestSession.HasValue)
                report.Add($"  Oldest Session: {stats.OldestSession:yyyy-MM-dd HH:mm:ss} UTC");

            if (stats.NewestSession.HasValue)
                report.Add($"  Newest Session: {stats.NewestSession:yyyy-MM-dd HH:mm:ss} UTC");

            if (stats.SessionsByAuthType.Any())
            {
                report.Add("");
                report.Add("Sessions by Auth Type:");
                foreach (var kvp in stats.SessionsByAuthType)
                {
                    report.Add($"  {kvp.Key}: {kvp.Value}");
                }
            }

            if (sessions.Any())
            {
                report.Add("");
                report.Add("Session Details:");
                foreach (var session in sessions.OrderBy(s => s.Name))
                {
                    var status = session.IsAuthenticated ? (session.IsExpired ? "Expired" : "Active") : "Not Auth";
                    report.Add($"  {session.Name} | {session.BaseUri} | {session.AuthType} | {status}");
                }
            }

            return string.Join(Environment.NewLine, report);
        }
    }
}
