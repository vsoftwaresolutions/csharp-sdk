using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ModelContextProtocol.AspNetCore;

internal sealed partial class StatefulSessionManager(
    IOptions<HttpServerTransportOptions> httpServerTransportOptions,
    ILogger<StatefulSessionManager> logger) : ConcurrentDictionary<string, StreamableHttpSession>(StringComparer.Ordinal)
{
    // Workaround for https://github.com/dotnet/runtime/issues/91121. This is fixed in .NET 9 and later.
    private readonly ILogger _logger = logger;

    private readonly TimeProvider _timeProvider = httpServerTransportOptions.Value.TimeProvider;
    private readonly TimeSpan _idleTimeout = httpServerTransportOptions.Value.IdleTimeout;
    private readonly long _idleTimeoutTicks = httpServerTransportOptions.Value.IdleTimeout.Ticks;
    private readonly int _maxIdleSessionCount = httpServerTransportOptions.Value.MaxIdleSessionCount;

    private readonly SemaphoreSlim _idlePruningLock = new(1, 1);
    private readonly List<long> _idleTimeStamps = [];
    private readonly List<string> _idleSessionIds = [];
    private int _nextIndexToPrune;

    private long _currentIdleSessionCount;

    public TimeProvider TimeProvider => _timeProvider;

    public void IncrementIdleSessionCount() => Interlocked.Increment(ref _currentIdleSessionCount);
    public void DecrementIdleSessionCount() => Interlocked.Decrement(ref _currentIdleSessionCount);

    public async ValueTask StartNewSessionAsync(StreamableHttpSession newSession, CancellationToken cancellationToken)
    {
        if (TryAddSessionImmediately(newSession))
        {
            return;
        }

        await _idlePruningLock.WaitAsync(cancellationToken);
        try
        {
            while (!TryAddSessionImmediately(newSession))
            {
                EnsureIdleSessionsSortedUnsynchronized();

                while (_nextIndexToPrune < _idleSessionIds.Count)
                {
                    var pruneId = _idleSessionIds[_nextIndexToPrune++];
                    if (TryGetValue(pruneId, out var sessionToPrune))
                    {
                        if (!sessionToPrune.IsActive && TryRemove(pruneId, out _))
                        {
                            LogIdleSessionLimit(pruneId, _maxIdleSessionCount);

                            // We're intentionally waiting for the idle session to be disposed before releasing the _idlePruningLock to
                            // ensure new sessions are not created faster than they're removed when we're at or above the maximum idle session count.
                            await DisposeSessionAsync(sessionToPrune);

                            // Take one last chance to check if the initialize request was aborted before we incur the cost of managing a new session.
                            cancellationToken.ThrowIfCancellationRequested();
                            AddSession(newSession);
                            return;
                        }
                    }
                }

                // If we couldn't find any active idle sessions to dispose, start another full prune to repopulate _idleSessionIds.
                PruneIdleSessionsUnsynchronized();

                if (_idleSessionIds.Count == 0 && Interlocked.Read(ref _currentIdleSessionCount) >= _maxIdleSessionCount)
                {
                    // This indicates all idle sessions are in the process of being disposed which should not happen during normal operation.
                    // Since there are no idle sessions to prune right now, log a critical error and create the new session anyway.
                    LogTooManyIdleSessionsClosingConcurrently(newSession.Id, _maxIdleSessionCount, Interlocked.Read(ref _currentIdleSessionCount));
                    AddSession(newSession);
                    return;
                }
            }
        }
        catch
        {
            await newSession.DisposeAsync();
            throw;
        }
        finally
        {
            _idlePruningLock.Release();
        }
    }

    /// <summary>
    /// Performs a single pass of idle session pruning, removing sessions that exceed the idle timeout
    /// or when the maximum idle session count is exceeded.
    /// </summary>
    public async Task PruneIdleSessionsAsync(CancellationToken cancellationToken)
    {
        await _idlePruningLock.WaitAsync(cancellationToken);
        try
        {
            PruneIdleSessionsUnsynchronized();
        }
        finally
        {
            _idlePruningLock.Release();
        }
    }

    private void PruneIdleSessionsUnsynchronized()
    {
        var idleActivityCutoff = _idleTimeoutTicks switch
        {
            < 0 => long.MinValue,
            var ticks => _timeProvider.GetTimestamp() - ticks,
        };

        // We clear the lists at the start of pruning rather than the end so we can use them between runs
        // to find the most idle sessions to remove one-at-a-time if necessary to make room for new sessions.
        _idleTimeStamps.Clear();
        _idleSessionIds.Clear();
        _nextIndexToPrune = -1;

        foreach (var (_, session) in this)
        {
            if (session.IsActive || session.SessionClosed.IsCancellationRequested)
            {
                // There's a request currently active or the session is already being closed.
                continue;
            }

            if (session.LastActivityTicks < idleActivityCutoff)
            {
                LogIdleSessionTimeout(session.Id, _idleTimeout);
                RemoveAndCloseSession(session.Id);
                continue;
            }

            // Add the timestamp and the session
            _idleTimeStamps.Add(session.LastActivityTicks);
            _idleSessionIds.Add(session.Id);
        }

        if (_idleTimeStamps.Count > _maxIdleSessionCount)
        {
            // Sort only if the maximum is breached and sort solely by the timestamp.
            EnsureIdleSessionsSortedUnsynchronized();

            var sessionsToPrune = CollectionsMarshal.AsSpan(_idleSessionIds)[..^_maxIdleSessionCount];
            foreach (var id in sessionsToPrune)
            {
                LogIdleSessionLimit(id, _maxIdleSessionCount);
                RemoveAndCloseSession(id);
            }
            _nextIndexToPrune = _maxIdleSessionCount;
        }
    }

    private void EnsureIdleSessionsSortedUnsynchronized()
    {
        if (_nextIndexToPrune > -1)
        {
            // Already sorted.
            return;
        }

        var timestamps = CollectionsMarshal.AsSpan(_idleTimeStamps);
        timestamps.Sort(CollectionsMarshal.AsSpan(_idleSessionIds));
        _nextIndexToPrune = 0;
    }

    /// <summary>
    /// Disposes all sessions in the manager, typically called during graceful shutdown.
    /// </summary>
    public async Task DisposeAllSessionsAsync()
    {
        List<Task> disposeSessionTasks = [];

        foreach (var (sessionKey, _) in this)
        {
            if (TryRemove(sessionKey, out var session))
            {
                disposeSessionTasks.Add(DisposeSessionAsync(session));
            }
        }

        await Task.WhenAll(disposeSessionTasks);
    }

    private bool TryAddSessionImmediately(StreamableHttpSession session)
    {
        if (Interlocked.Read(ref _currentIdleSessionCount) < _maxIdleSessionCount)
        {
            AddSession(session);
            return true;
        }

        return false;
    }

    private void AddSession(StreamableHttpSession session)
    {
        if (!TryAdd(session.Id, session))
        {
            throw new UnreachableException($"Unreachable given good entropy! Session with ID '{session.Id}' has already been created.");
        }
    }

    private void RemoveAndCloseSession(string sessionId)
    {
        if (!TryRemove(sessionId, out var session))
        {
            return;
        }

        // Don't slow down the idle tracking loop. DisposeSessionAsync logs. We only await during graceful shutdown.
        _ = DisposeSessionAsync(session);
    }

    private async Task DisposeSessionAsync(StreamableHttpSession session)
    {
        try
        {
            await session.DisposeAsync();
        }
        catch (Exception ex)
        {
            LogSessionDisposeError(session.Id, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "IdleTimeout of {IdleTimeout} exceeded. Closing idle session {SessionId}.")]
    private partial void LogIdleSessionTimeout(string sessionId, TimeSpan idleTimeout);

    [LoggerMessage(Level = LogLevel.Information, Message = "MaxIdleSessionCount of {MaxIdleSessionCount} exceeded. Closing idle session {SessionId} despite it being active more recently than the configured IdleTimeout to make room for new sessions.")]
    private partial void LogIdleSessionLimit(string sessionId, int maxIdleSessionCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error disposing session {SessionId}.")]
    private partial void LogSessionDisposeError(string sessionId, Exception ex);

    [LoggerMessage(Level = LogLevel.Critical, Message = "MaxIdleSessionCount of {MaxIdleSessionCount} exceeded, and {CurrentIdleSessionCount} sessions are currently in the process of closing. Creating new session {SessionId} anyway.")]
    private partial void LogTooManyIdleSessionsClosingConcurrently(string sessionId, int maxIdleSessionCount, long currentIdleSessionCount);
}
