#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTaskPlus.Internal;
using UnityEngine.Assertions;

namespace UniTaskPlus
{
    /// <summary>
    /// A <see cref="SemaphoreSlim"/> implementation for UniTask.
    /// </summary>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// using System;
    /// using Cysharp.Threading.Tasks;
    /// using UniTaskPlus;
    /// using UnityEngine;
    ///
    /// public class SemaphoreExample : MonoBehaviour
    /// {
    ///     private async UniTaskVoid Start()
    ///     {
    ///         // Semaphore that allows a maximum of 2 concurrent operations
    ///         var semaphore = new UniTaskSemaphore(2, 2);
    ///
    ///         // Start 5 workers in parallel, but allow a maximum of 2 concurrent executions
    ///         var tasks = new UniTask[5];
    ///         for (int i = 0; i < 5; i++)
    ///         {
    ///             int idx = i;
    ///             tasks[i] = WorkerAsync(idx, semaphore);
    ///         }
    ///
    ///         await UniTask.WhenAll(tasks);
    ///     }
    ///
    ///     private async UniTask WorkerAsync(int id, UniTaskSemaphore sem)
    ///     {
    ///         // WaitScopeAsync returns an IDisposable handle for release, so it can be automatically released with using
    ///         using (await sem.WaitScopeAsync())
    ///         {
    ///             Debug.Log($"Start {id}");
    ///             await UniTask.Delay(TimeSpan.FromSeconds(1));
    ///             Debug.Log($"End {id}");
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    [DebuggerDisplay("Current Count = {CurrentCount}")]
    public sealed class UniTaskSemaphore : IDisposable
    {
        /// <summary>
        /// The number of remaining tasks that can pass through (available slots).
        /// </summary>
        /// <seealso cref="SemaphoreSlim.CurrentCount"/>
        public uint CurrentCount { get; private set; }

        private readonly uint _maxCount;

        private UniTaskNode<bool>? _asyncHead;

        private UniTaskNode<bool>? _asyncTail;

        private bool _isDisposed;

        /// <summary>
        /// Initialize a new instance of the <see cref="UniTaskSemaphore"/> class.
        /// </summary>
        /// <param name="initialCount">The initial number of tasks allowed to proceed simultaneously.</param>
        /// <param name="maxCount">The maximum number of tasks allowed to proceed simultaneously.</param>
        /// <exception cref="ArgumentOutOfRangeException">initialCount is greater than maxCount, or maxCount is 0.</exception>
        /// <seealso cref="SemaphoreSlim(int)"/>
        /// <seealso cref="SemaphoreSlim(int,int)"/>
        public UniTaskSemaphore(uint initialCount, uint maxCount = uint.MaxValue)
        {
            if (initialCount > maxCount)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCount), initialCount, "The initialCount argument must be non-negative and less than or equal to the maximumCount.");
            }

            if (maxCount == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "The maximumCount argument must be a positive number. If a maximum is not required, use the constructor without a maxCount parameter.");
            }

            CurrentCount = initialCount;
            _maxCount = maxCount;
        }

        /// <summary>
        /// Asynchronously waits for the semaphore.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous wait operation.</returns>
        public UniTask WaitAsync(in CancellationToken cancellationToken = default)
        {
            return WaitAsync(Timeout.Infinite, cancellationToken).AsUniTask();
        }

        /// <summary>
        /// Asynchronously waits for the semaphore and gets a release handle.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous wait operation and returns a release handle.</returns>
        public async UniTask<Handle> WaitScopeAsync(CancellationToken cancellationToken = default)
        {
            await WaitAsync(Timeout.Infinite, cancellationToken);
            return new Handle(this);
        }

        /// <summary>
        /// Asynchronously waits for the semaphore with timeout.
        /// </summary>
        /// <param name="timeout">The timeout duration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that returns true if the semaphore was acquired successfully, false if it timed out.</returns>
        /// <exception cref="ArgumentOutOfRangeException">timeout is outside the valid range.</exception>
        public UniTask<bool> WaitAsync(in TimeSpan timeout, in CancellationToken cancellationToken = default)
        {
            var totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The timeout must represent a value between -1 and Int32.MaxValue, inclusive.");
            }

            return WaitAsync((int)timeout.TotalMilliseconds, cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits for the semaphore with timeout in milliseconds.
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout in milliseconds.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that returns true if the semaphore was acquired successfully, false if it timed out.</returns>
        /// <exception cref="ObjectDisposedException">The semaphore has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">millisecondsTimeout is outside the valid range.</exception>
        public UniTask<bool> WaitAsync(in int millisecondsTimeout, in CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(UniTaskSemaphore));
            }

            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), millisecondsTimeout, "The timeout must represent a value between -1 and Int32.MaxValue, inclusive.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromCanceled<bool>(cancellationToken);
            }

            if (CurrentCount > 0)
            {
                CurrentCount--;
                return UniTask.FromResult(true);
            }

            if (millisecondsTimeout == 0)
            {
                return UniTask.FromResult(false);
            }

            Assert.IsTrue(CurrentCount == 0, "CurrentCount should never be negative");
            var asyncWaiter = CreateAndAddAsyncWaiter();
            return millisecondsTimeout == Timeout.Infinite && !cancellationToken.CanBeCanceled
                ? asyncWaiter.WaitAsync(Timeout.Infinite)
                : WaitUntilCountOrTimeoutAsync(asyncWaiter, millisecondsTimeout, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UniTaskNode<bool> CreateAndAddAsyncWaiter()
        {
            var node = UniTaskNode<bool>.Create();

            if (_asyncHead == null)
            {
                Assert.IsTrue(_asyncTail == null, "If head is null, so too should be tail");
                _asyncHead = node;
                _asyncTail = node;
            }
            else
            {
                Assert.IsFalse(_asyncTail == null, "If head is not null, neither should be tail");
                _asyncTail!.Next = node;
                node.Prev = _asyncTail;
                _asyncTail = node;
            }

            return node;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool RemoveAsyncWaiter(UniTaskNode<bool> node)
        {
            Assert.IsFalse(node == null, "Expected non-null task");

            var wasInList = _asyncHead == node || node!.Prev != null;

            if (node!.Next != null)
            {
                node.Next.Prev = node.Prev;
            }
            if (node.Prev != null)
            {
                node.Prev.Next = node.Next;
            }
            if (_asyncHead == node)
            {
                _asyncHead = node.Next;
            }
            if (_asyncTail == node)
            {
                _asyncTail = node.Prev;
            }
            Assert.IsTrue((_asyncHead, _asyncTail) is (null, null) or (not null, not null), "Head is null iff tail is null");

            node.Next = node.Prev = null;

            return wasInList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async UniTask<bool> WaitUntilCountOrTimeoutAsync(UniTaskNode<bool> asyncWaiter, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            Assert.IsFalse(asyncWaiter == null, "Waiter should have been constructed");

            using var registration = cancellationToken.RegisterWithoutCaptureExecutionContext(static self => ((UniTaskNode<bool>)self!).TrySetCanceled(), asyncWaiter);

            try
            {
                await asyncWaiter!.WaitAsync(millisecondsTimeout);
                return true;
            }
            catch (Exception)
            {
                if (RemoveAsyncWaiter(asyncWaiter!))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return false;
                }

                throw;
            }
        }

        /// <summary>
        /// Releases the semaphore.
        /// </summary>
        /// <param name="releaseCount">The number of semaphore slots to release.</param>
        /// <returns>The previous count.</returns>
        /// <exception cref="ObjectDisposedException">The semaphore has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">releaseCount is less than 1.</exception>
        /// <exception cref="SemaphoreFullException">The semaphore is already at maximum capacity.</exception>
        public uint Release(uint releaseCount = 1)
        {
            if (_isDisposed)
            {
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    goto ForceRelease;
                }
#endif
                throw new ObjectDisposedException(nameof(UniTaskSemaphore));
            }

ForceRelease:
            if (releaseCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(releaseCount), releaseCount, "The releaseCount argument must be greater than zero.");
            }

            var currentCount = CurrentCount;
            var returnCount = currentCount;

            if (_maxCount - currentCount < releaseCount)
            {
                throw new SemaphoreFullException();
            }

            currentCount += releaseCount;

            if (_asyncHead != null)
            {
                Assert.IsFalse(_asyncTail == null, "tail should not be null if head isn't null");
                while (currentCount > 0 && _asyncHead != null)
                {
                    --currentCount;

                    var waiterTask = _asyncHead;
                    RemoveAsyncWaiter(waiterTask);
                    waiterTask.TrySetResult(true);
                }
            }
            CurrentCount = currentCount;

            return returnCount;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing"></param>
        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isDisposed = true;
                _asyncHead = null;
                _asyncTail = null;
            }
        }

        /// <summary>
        /// Release handle for the semaphore.
        /// </summary>
        public readonly struct Handle : IDisposable
        {
            private readonly UniTaskSemaphore _semaphore;

            internal Handle(UniTaskSemaphore semaphore) => _semaphore = semaphore;

            void IDisposable.Dispose() => _semaphore.Release();
        }
    }
}