namespace ModelContextProtocol.Utils;

/// <summary>
/// Provides a thread-safe synchronized value with locking functionality.
/// </summary>
/// <typeparam name="T">The type of value to synchronize.</typeparam>
internal class SynchronizedValue<T> where T : class
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private T _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="SynchronizedValue{T}"/> class.
    /// </summary>
    /// <param name="initialValue">The initial value.</param>
    public SynchronizedValue(T initialValue)
    {
        _value = initialValue;
    }

    /// <summary>
    /// Gets the current value without locking.
    /// </summary>
    /// <remarks>
    /// This property should only be used when thread safety is not required.
    /// </remarks>
    public T UnsafeValue => _value;

    /// <summary>
    /// Acquires a lock on the value and provides access to it.
    /// </summary>
    /// <returns>A disposable <see cref="SynchronizedValueHandle"/> that provides access to the value and releases the lock when disposed.</returns>
    public async Task<SynchronizedValueHandle> LockAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        return new SynchronizedValueHandle(this);
    }

    /// <summary>
    /// Provides a handle to access the synchronized value while holding a lock.
    /// </summary>
    public class SynchronizedValueHandle : IDisposable
    {
        private readonly SynchronizedValue<T> _parent;
        private bool _disposed;

        internal SynchronizedValueHandle(SynchronizedValue<T> parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Gets or sets the synchronized value.
        /// </summary>
        public T Value
        {
            get => _parent._value;
            set => _parent._value = value;
        }

        /// <summary>
        /// Releases the lock on the synchronized value.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _parent._semaphore.Release();
        }
    }
}