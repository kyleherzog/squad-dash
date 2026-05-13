using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SquadDash;

internal sealed class MutexLease : IDisposable {
    // Tracks (name, threadId) keys for mutexes currently held to prevent same-thread
    // re-entrant acquisition without blocking different threads acquiring the same mutex.
    private static readonly ConcurrentDictionary<string, byte> s_heldNames = new();

    private Mutex? _mutex;
    private readonly string _name;

    private MutexLease(Mutex mutex, string name) {
        _mutex = mutex;
        _name = name;
    }

    public static MutexLease Acquire(string name) {
        if (!TryAcquire(name, Timeout.InfiniteTimeSpan, out var lease) || lease is null)
            throw new TimeoutException($"Timed out waiting to acquire mutex '{name}'.");

        return lease;
    }

    public static bool TryAcquire(string name, out MutexLease? lease) {
        return TryAcquire(name, TimeSpan.Zero, out lease);
    }

    public static bool TryAcquire(string name, TimeSpan timeout, out MutexLease? lease) {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Mutex name cannot be empty.", nameof(name));

        if (timeout < Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var normalizedName = name.ToLowerInvariant();
        var threadKey = $"{normalizedName}:{Environment.CurrentManagedThreadId}";

        // Reject re-entrant acquisition on the same thread; different threads get a unique key
        // and will block normally on WaitOne rather than failing immediately.
        if (!s_heldNames.TryAdd(threadKey, 0)) {
            lease = null;
            return false;
        }

        var mutex = new Mutex(false, name);
        var acquired = false;

        try {
            acquired = mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException) {
            // Ownership transfers to this thread when the previous owner exits unexpectedly.
            acquired = true;
        }

        if (!acquired) {
            s_heldNames.TryRemove(threadKey, out _);
            mutex.Dispose();
            lease = null;
            return false;
        }

        lease = new MutexLease(mutex, threadKey);
        return true;
    }

    public void Dispose() {
        var mutex = Interlocked.Exchange(ref _mutex, null);
        if (mutex is null)
            return;

        try {
            mutex.ReleaseMutex();
        }
        finally {
            s_heldNames.TryRemove(_name, out _);
            mutex.Dispose();
        }
    }
}
