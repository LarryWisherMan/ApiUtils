// File: ApiUtils/Services/FileSessionStore.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApiUtils.Interfaces;
using ApiUtils.Models;

#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace ApiUtils.Services
{
    /// <summary>
    /// File-based session store implementation
    /// </summary>
    public class FileSessionStore : ISessionStore
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public FileSessionStore(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ApiUtils", "sessions.json");

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
        }

        public async Task<bool> AddSessionAsync(ApiSession session)
        {
            if (session == null) return false;

            await _fileLock.WaitAsync();
            try
            {
                var sessions = await LoadSessionsFromFileAsync();

                if (sessions.ContainsKey(session.Name))
                    return false;

                sessions[session.Name] = CreateSerializableSession(session);
                await SaveSessionsToFileAsync(sessions);
                return true;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<ApiSession> GetSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            await _fileLock.WaitAsync();
            try
            {
                var sessions = await LoadSessionsFromFileAsync();

                if (sessions.TryGetValue(name, out var sessionData))
                {
                    return CreateApiSession(sessionData);
                }

                return null;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<bool> RemoveSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            await _fileLock.WaitAsync();
            try
            {
                var sessions = await LoadSessionsFromFileAsync();

                if (sessions.Remove(name))
                {
                    await SaveSessionsToFileAsync(sessions);
                    return true;
                }

                return false;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<IEnumerable<ApiSession>> GetAllSessionsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var sessions = await LoadSessionsFromFileAsync();

                return sessions.Values
                    .Select(CreateApiSession)
                    .Where(s => s != null && !s.IsExpired)
                    .ToList();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<bool> ClearAllSessionsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                await SaveSessionsToFileAsync(new Dictionary<string, SerializableSession>());
                return true;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<bool> SessionExistsAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var session = await GetSessionAsync(name);
            return session != null && !session.IsExpired;
        }

        public async Task<int> CleanupExpiredSessionsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var sessions = await LoadSessionsFromFileAsync();
                var expiredKeys = sessions
                    .Where(kvp => DateTime.UtcNow > kvp.Value.ExpiresAt)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    sessions.Remove(key);
                }

                if (expiredKeys.Any())
                {
                    await SaveSessionsToFileAsync(sessions);
                }

                return expiredKeys.Count;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task<Dictionary<string, SerializableSession>> LoadSessionsFromFileAsync()
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, SerializableSession>();

            try
            {
#if NET6_0_OR_GREATER
                var json = await File.ReadAllTextAsync(_filePath);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                return JsonSerializer.Deserialize<Dictionary<string, SerializableSession>>(json, options)
                    ?? new Dictionary<string, SerializableSession>();
#else
                var json = await ReadAllTextAsync(_filePath);
                return JsonConvert.DeserializeObject<Dictionary<string, SerializableSession>>(json)
                    ?? new Dictionary<string, SerializableSession>();
#endif
            }
            catch
            {
                return new Dictionary<string, SerializableSession>();
            }
        }

        private async Task SaveSessionsToFileAsync(Dictionary<string, SerializableSession> sessions)
        {
#if NET6_0_OR_GREATER
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(sessions, options);
            await File.WriteAllTextAsync(_filePath, json);
#else
            var json = JsonConvert.SerializeObject(sessions, Formatting.Indented);
            await WriteAllTextAsync(_filePath, json);
#endif
        }

#if !NET6_0_OR_GREATER
        // Helper methods for .NET Framework 4.8 compatibility
        private async Task<string> ReadAllTextAsync(string path)
        {
            using (var reader = new StreamReader(path))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private async Task WriteAllTextAsync(string path, string content)
        {
            using (var writer = new StreamWriter(path))
            {
                await writer.WriteAsync(content);
            }
        }
#endif

        private SerializableSession CreateSerializableSession(ApiSession session)
        {
            return new SerializableSession
            {
                Name = session.Name,
                BaseUri = session.BaseUri.ToString(),
                AuthType = session.AuthType,
                IsAuthenticated = session.IsAuthenticated,
                CreatedAt = session.CreatedAt,
                LastAuthenticated = session.LastAuthenticated,
                ExpiresAt = session.ExpiresAt,
                TimeoutMinutes = session.TimeoutMinutes,
                Headers = session.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        private ApiSession CreateApiSession(SerializableSession sessionData)
        {
            try
            {
                var session = new ApiSession(sessionData.Name, new Uri(sessionData.BaseUri), sessionData.AuthType)
                {
                    IsAuthenticated = sessionData.IsAuthenticated,
                    LastAuthenticated = sessionData.LastAuthenticated,
                    TimeoutMinutes = sessionData.TimeoutMinutes
                };

                // Copy headers
                foreach (var header in sessionData.Headers)
                {
                    session.Headers[header.Key] = header.Value;
                }

                return session;
            }
            catch
            {
                return null;
            }
        }

        private class SerializableSession
        {
            public string Name { get; set; }
            public string BaseUri { get; set; }
            public AuthenticationType AuthType { get; set; }
            public bool IsAuthenticated { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? LastAuthenticated { get; set; }
            public DateTime ExpiresAt { get; set; }
            public int TimeoutMinutes { get; set; }
            public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        }
    }
}
