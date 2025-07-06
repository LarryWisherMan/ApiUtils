#nullable enable
using System;
using System.Collections.Concurrent;
using ApiUtils.Models;
using System.Diagnostics.CodeAnalysis;


namespace ApiUtils.Runtime;

/// <summary>
/// Thread-safe global registry for managing live <see cref="ApiSession"/> objects.
/// </summary>
internal static class SessionRegistry
{
    private static readonly ConcurrentDictionary<string, ApiSession> _store =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds or replaces a session by name.
    /// </summary>
    /// <param name="session">The session to store.</param>
    /// <exception cref="ArgumentNullException"/>
    public static void Add(ApiSession session)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));
        _store[session.Info.Name] = session;
    }

    /// <summary>
    /// Attempts to retrieve a session by name.
    /// </summary>
    /// <param name="name">The session name.</param>
    /// <param name="session">The resulting session, if found.</param>
    /// <returns>True if the session exists; otherwise, false.</returns>
    public static bool TryGet(string name, [NotNullWhen(true)] out ApiSession? session)
        => _store.TryGetValue(name, out session);

    /// <summary>
    /// Removes a session by name and disposes it.
    /// </summary>
    /// <param name="name">The session name to remove.</param>
    /// <returns>True if removed; otherwise, false.</returns>
    public static bool Remove(string name)
    {
        if (_store.TryRemove(name, out var session))
        {
            session.Dispose();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all sessions and disposes them.
    /// </summary>
    public static void Clear()
    {
        foreach (var session in _store.Values)
            session.Dispose();

        _store.Clear();
    }
}
