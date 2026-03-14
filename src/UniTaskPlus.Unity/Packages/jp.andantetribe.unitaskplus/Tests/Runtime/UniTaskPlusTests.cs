#nullable enable

using System;
using System.Buffers;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniTaskPlus;
using UniTaskPlus.Internal;
using UnityEngine.TestTools;

namespace UniTaskPlus.Tests.Runtime
{
    #region Internal Class Coverage Tests

    public class ArrayPoolExtensionsTests
    {
        [Test]
        public void GrowWhenArrayIsEmptyRentsNewArray()
        {
            var pool = ArrayPool<int>.Shared;
            int[] array = Array.Empty<int>();

            pool.Grow(ref array, 10);

            Assert.That(array.Length, Is.GreaterThanOrEqualTo(10));
            pool.Return(array);
        }

        [Test]
        public void GrowWhenArrayHasContentCopiesAndClears()
        {
            var pool = ArrayPool<int>.Shared;
            int[] array = pool.Rent(2);
            array[0] = 100;
            array[1] = 200;

            pool.Grow(ref array, 10);

            Assert.That(array[0], Is.EqualTo(100));
            Assert.That(array[1], Is.EqualTo(200));
            Assert.That(array.Length, Is.GreaterThanOrEqualTo(10));

            pool.Return(array);
        }
    }

    public class StateTupleTests
    {
        [Test]
        public void StateTuple1CreateAndDeconstructUsesPool()
        {
            var tuple = StateTuple.Create(1);
            tuple.Deconstruct(out var item1);
            Assert.That(item1, Is.EqualTo(1));

            var tuple2 = StateTuple.Create(2);
            tuple2.Deconstruct(out var item2);
            Assert.That(item2, Is.EqualTo(2));
        }

        [Test]
        public void StateTuple2CreateAndDeconstructUsesPool()
        {
            var tuple = StateTuple.Create(1, "A");
            tuple.Deconstruct(out var i1, out var s1);
            Assert.That(i1, Is.EqualTo(1));
            Assert.That(s1, Is.EqualTo("A"));

            var tuple2 = StateTuple.Create(2, "B");
            tuple2.Deconstruct(out var i2, out var s2);
            Assert.That(i2, Is.EqualTo(2));
            Assert.That(s2, Is.EqualTo("B"));
        }

        [Test]
        public void StateTuple3CreateAndDeconstructUsesPool()
        {
            var tuple = StateTuple.Create(1, "A", 0.5f);
            tuple.Deconstruct(out var i1, out var s1, out var f1);
            Assert.That(i1, Is.EqualTo(1));
            Assert.That(s1, Is.EqualTo("A"));
            Assert.That(f1, Is.EqualTo(0.5f));

            var tuple2 = StateTuple.Create(2, "B", 1.5f);
            tuple2.Deconstruct(out var i2, out var s2, out var f2);
            Assert.That(i2, Is.EqualTo(2));
            Assert.That(s2, Is.EqualTo("B"));
            Assert.That(f2, Is.EqualTo(1.5f));
        }
    }

    public class UniTaskNodeTests
    {
        [UnityTest]
        public IEnumerator NodeTrySetExceptionSetsFaultedStatus() => UniTask.ToCoroutine(async () =>
        {
            var node = UniTaskNode<bool>.Create();
            var task = node.WaitAsync(Timeout.Infinite);

            Assert.That(node.UnsafeGetStatus(), Is.EqualTo(UniTaskStatus.Pending));

            node.TrySetException(new InvalidOperationException());

            // awaitで消費される前にステータスを確認
            Assert.That(node.UnsafeGetStatus(), Is.EqualTo(UniTaskStatus.Faulted));

            try
            {
                await task;
                Assert.Fail("Exception should have been thrown.");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        });

        [UnityTest]
        public IEnumerator NodeTimeoutThrowsOperationCanceledException() => UniTask.ToCoroutine(async () =>
        {
            var node = UniTaskNode<bool>.Create();
            var task = node.WaitAsync(10);

            // タイムアウトを待機
            await UniTask.Delay(50);

            // awaitで消費される前にステータスを確認
            Assert.That(node.UnsafeGetStatus(), Is.EqualTo(UniTaskStatus.Canceled));

            try
            {
                await task;
                Assert.Fail("Timeout should have occurred.");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });

        [UnityTest]
        public IEnumerator NodeExternalCancellationThrowsOperationCanceledException() => UniTask.ToCoroutine(async () =>
        {
            var cts = new CancellationTokenSource();
            var node = UniTaskNode<bool>.Create();
            var task = node.WaitAsync(Timeout.Infinite, cts.Token);

            cts.Cancel();

            try
            {
                await task;
                Assert.Fail("Cancellation should have occurred.");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });

        [UnityTest]
        public IEnumerator NodeTrySetResultReturnsExpectedValue() => UniTask.ToCoroutine(async () =>
        {
            var node1 = UniTaskNode<int>.Create();
            node1.Next = null;
            node1.Prev = null;

            var task1 = node1.WaitAsync(Timeout.Infinite);
            node1.TrySetResult(42);

            var result = await task1;
            Assert.That(result, Is.EqualTo(42));

            var node2 = UniTaskNode<int>.Create();
            var task2 = node2.WaitAsync(Timeout.Infinite);
            node2.TrySetResult(99);

            var result2 = await task2;
            Assert.That(result2, Is.EqualTo(99));

            // IUniTaskSource の非ジェネリック GetResult メソッドのカバー
            var node3 = UniTaskNode<int>.Create();
            var task3 = node3.WaitAsync(Timeout.Infinite);
            UniTask untypedTask = task3; // 非ジェネリックへキャスト
            node3.TrySetResult(100);
            await untypedTask; // 内部で IUniTaskSource.GetResult が呼ばれる
        });
    }

    #endregion

    #region UniTaskBag Tests

    public class UniTaskBagTests
    {
        [UnityTest]
        public IEnumerator DisposeAsyncWhenEmptyReturnsCompletedTask() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator AddSingleTaskCompletesOnDispose() => UniTask.ToCoroutine(async () =>
        {
            var completed = false;
            var bag = new UniTaskBag();
            bag.Add(UniTask.RunOnThreadPool(() => completed = true));
            await bag.DisposeAsync();
            Assert.That(completed, Is.True);
        });

        [UnityTest]
        public IEnumerator AddTwoTasksTriggersGrow() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            var bag = new UniTaskBag();
            bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
            bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
            await bag.DisposeAsync();
            Assert.That(count, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator AddMultipleTasksAllCompleteOnDispose() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            var bag = new UniTaskBag();
            for (var i = 0; i < 5; i++)
            {
                bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
            }
            await bag.DisposeAsync();
            Assert.That(count, Is.EqualTo(5));
        });

        [UnityTest]
        public IEnumerator AddManyTasksGrowsMultipleTimes() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            var bag = new UniTaskBag();
            for (var i = 0; i < 20; i++)
            {
                bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
            }
            await bag.DisposeAsync();
            Assert.That(count, Is.EqualTo(20));
        });

        [UnityTest]
        public IEnumerator AwaitUsingPattern() => UniTask.ToCoroutine(async () =>
        {
            var completed = false;
            await using (var bag = new UniTaskBag())
            {
                bag.Add(UniTask.RunOnThreadPool(() => completed = true));
            }
            Assert.That(completed, Is.True);
        });

        [UnityTest]
        public IEnumerator DisposeAsyncCalledTwiceNoError() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            bag.Add(UniTask.CompletedTask);
            await bag.DisposeAsync();
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator AddDelayedTasksAllCompleteOnDispose() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            var bag = new UniTaskBag();
            for (var i = 0; i < 3; i++)
            {
                bag.Add(UniTask.RunOnThreadPool(async () =>
                {
                    await UniTask.Delay(10);
                    Interlocked.Increment(ref count);
                }));
            }
            await bag.DisposeAsync();
            Assert.That(count, Is.EqualTo(3));
        });

        [UnityTest]
        public IEnumerator AddLargeNumberOfCompletedTasks() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            for (var i = 0; i < 100; i++)
            {
                bag.Add(UniTask.CompletedTask);
            }
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator AwaitUsingMultipleTasksPattern() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            await using (var bag = new UniTaskBag())
            {
                for (var i = 0; i < 10; i++)
                {
                    bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
                }
            }
            Assert.That(count, Is.EqualTo(10));
        });

        [UnityTest]
        public IEnumerator DisposeAsyncWhenNoTasksAddedReturnsImmediately() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator AddExactlyOneTaskUsesRentPath() => UniTask.ToCoroutine(async () =>
        {
            var completed = false;
            var bag = new UniTaskBag();
            bag.Add(UniTask.RunOnThreadPool(() => completed = true));
            await bag.DisposeAsync();
            Assert.That(completed, Is.True);
        });
    }

    #endregion

    #region UniTaskSemaphore Constructor Tests

    public class UniTaskSemaphoreConstructorTests
    {
        [Test]
        public void ConstructorValidParametersSetCurrentCount()
        {
            var sem = new UniTaskSemaphore(3, 5);
            Assert.That(sem.CurrentCount, Is.EqualTo(3u));
        }

        [Test]
        public void ConstructorDefaultMaxCountSetCurrentCount()
        {
            var sem = new UniTaskSemaphore(1);
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        }

        [Test]
        public void ConstructorInitialCountGreaterThanMaxCountThrows()
        {
            Assert.That(() => new UniTaskSemaphore(5, 3), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ConstructorMaxCountZeroThrows()
        {
            Assert.That(() => new UniTaskSemaphore(0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ConstructorZeroInitialCountValid()
        {
            var sem = new UniTaskSemaphore(0, 5);
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        }

        [Test]
        public void ConstructorInitialEqualsMaxCountValid()
        {
            var sem = new UniTaskSemaphore(10, 10);
            Assert.That(sem.CurrentCount, Is.EqualTo(10u));
        }

        [Test]
        public void ConstructorDefaultMaxCountAllowsRelease()
        {
            var sem = new UniTaskSemaphore(0);
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
            sem.Release();
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        }

        [Test]
        public void ConstructorLargeMaxCountValid()
        {
            var sem = new UniTaskSemaphore(100, 1000);
            Assert.That(sem.CurrentCount, Is.EqualTo(100u));
        }
    }

    #endregion

    #region UniTaskSemaphore WaitAsync Tests

    public class UniTaskSemaphoreWaitAsyncTests
    {
        [UnityTest]
        public IEnumerator WaitAsyncCountAvailableDecrementsAndReturnsTrue() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(2, 2);
            var result = await sem.WaitAsync(Timeout.Infinite);
            Assert.That(result, Is.True);
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator WaitAsyncZeroTimeoutWhenNoCountReturnsFalse() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(0);
            Assert.That(result, Is.False);
        });

        [UnityTest]
        public IEnumerator WaitAsyncDisposedThrowsObjectDisposedException() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            sem.Dispose();
            try
            {
                await sem.WaitAsync(Timeout.Infinite);
                Assert.Fail("Should have thrown");
            }
            catch (ObjectDisposedException)
            {
                // expected
            }
        });

        [Test]
        public void WaitAsyncNegativeTimeoutThrows()
        {
            var sem = new UniTaskSemaphore(1, 1);
            Assert.That(() => sem.WaitAsync(-2).Forget(), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [UnityTest]
        public IEnumerator WaitAsyncCancellationAlreadyRequestedReturnsCanceled() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var cts = new CancellationTokenSource();
            cts.Cancel();
            try
            {
                await sem.WaitAsync(Timeout.Infinite, cts.Token);
                Assert.Fail("Should have been canceled");
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        });

        [UnityTest]
        public IEnumerator WaitAsyncWithTimeSpanValid() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var result = await sem.WaitAsync(TimeSpan.FromMilliseconds(1000));
            Assert.That(result, Is.True);
        });

        [Test]
        public void WaitAsyncWithTimeSpanInvalidRangeThrows()
        {
            var sem = new UniTaskSemaphore(1, 1);
            Assert.That(
                () => sem.WaitAsync(TimeSpan.FromMilliseconds(-2)).Forget(),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void WaitAsyncWithTimeSpanTooLargeThrows()
        {
            var sem = new UniTaskSemaphore(1, 1);
            Assert.That(
                () => sem.WaitAsync(TimeSpan.FromMilliseconds((double)int.MaxValue + 1)).Forget(),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [UnityTest]
        public IEnumerator WaitAsyncInfiniteTimeoutNoCancellationDirectWaitPath() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var waitTask = sem.WaitAsync(Timeout.Infinite);
            await UniTask.Delay(50);
            sem.Release();
            await waitTask;
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        });

        [UnityTest]
        public IEnumerator WaitAsyncInfiniteTimeoutWithCancellableToken() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var cts = new CancellationTokenSource();
            var waitTask = sem.WaitAsync(Timeout.Infinite, cts.Token);
            await UniTask.Delay(50);
            sem.Release();
            var result = await waitTask;
            Assert.That(result, Is.True);
        });

        [UnityTest]
        public IEnumerator WaitAsyncFiniteTimeoutReleasedReturnsTrue() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var waitTask = sem.WaitAsync(5000);
            await UniTask.Delay(50);
            sem.Release();
            var result = await waitTask;
            Assert.That(result, Is.True);
        });

        [UnityTest]
        public IEnumerator WaitAsyncFiniteTimeoutTimesOutReturnsFalse() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(50);
            Assert.That(result, Is.False);
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        });

        [UnityTest]
        public IEnumerator WaitAsyncCancellationWhileWaitingThrowsCancellation() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var cts = new CancellationTokenSource();
            var waitTask = sem.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);
            await UniTask.Delay(50);
            cts.Cancel();
            try
            {
                await waitTask;
                Assert.Fail("Should have been canceled");
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        });

        [UnityTest]
        public IEnumerator WaitAsyncDefaultNoParameters() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            await sem.WaitAsync();
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        });

        [UnityTest]
        public IEnumerator WaitAsyncNoCancelInfiniteTimeoutWaitsAndCompletes() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var waited = false;

            async UniTask WaitTask()
            {
                await sem.WaitAsync();
                waited = true;
            }

            var waitTask = WaitTask();
            await UniTask.Delay(50);
            Assert.That(waited, Is.False);
            sem.Release();
            await waitTask;
            Assert.That(waited, Is.True);
        });

        [UnityTest]
        public IEnumerator WaitAsyncWithCancellableTokenButCompletes() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var cts = new CancellationTokenSource();
            var waitTask = sem.WaitAsync(Timeout.Infinite, cts.Token);
            await UniTask.Delay(50);
            sem.Release();
            var result = await waitTask;
            Assert.That(result, Is.True);
        });

        [UnityTest]
        public IEnumerator WaitAsyncWithTimeSpanInfiniteTimeoutValid() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var result = await sem.WaitAsync(TimeSpan.FromMilliseconds(-1));
            Assert.That(result, Is.True);
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        });

        [UnityTest]
        public IEnumerator WaitAsyncWithTimeSpanZeroTimeoutCountAvailable() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var result = await sem.WaitAsync(TimeSpan.Zero);
            Assert.That(result, Is.True);
        });

        [UnityTest]
        public IEnumerator WaitAsyncWithTimeSpanZeroTimeoutNoCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(TimeSpan.Zero);
            Assert.That(result, Is.False);
        });

        [UnityTest]
        public IEnumerator WaitAsyncMultipleDecrementsToZero() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(3, 3);
            await sem.WaitAsync(Timeout.Infinite);
            await sem.WaitAsync(Timeout.Infinite);
            await sem.WaitAsync(Timeout.Infinite);
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        });

        [UnityTest]
        public IEnumerator WaitAsyncDisposedWithCancellationTokenThrows() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            sem.Dispose();
            var cts = new CancellationTokenSource();
            try
            {
                await sem.WaitAsync(Timeout.Infinite, cts.Token);
                Assert.Fail("Should have thrown");
            }
            catch (ObjectDisposedException)
            {
                // expected
            }
        });

        [UnityTest]
        public IEnumerator WaitAsyncCountAvailableWithCancellableToken() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(2, 2);
            var cts = new CancellationTokenSource();
            var result = await sem.WaitAsync(Timeout.Infinite, cts.Token);
            Assert.That(result, Is.True);
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator WaitAsyncTimeoutReturnsFalse() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(50);
            Assert.That(result, Is.False);
        });
    }

    #endregion

    #region UniTaskSemaphore Release Tests

    public class UniTaskSemaphoreReleaseTests
    {
        [UnityTest]
        public IEnumerator ReleaseIncrementsCurrentCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 2);
            await sem.WaitAsync(Timeout.Infinite);
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
            var prev = sem.Release();
            Assert.That(prev, Is.EqualTo(0u));
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator ReleaseMultipleSlots() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(3, 5);
            await sem.WaitAsync(Timeout.Infinite);
            await sem.WaitAsync(Timeout.Infinite);
            await sem.WaitAsync(Timeout.Infinite);
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
            var prev = sem.Release(3);
            Assert.That(prev, Is.EqualTo(0u));
            Assert.That(sem.CurrentCount, Is.EqualTo(3u));
        });

        [Test]
        public void ReleaseWhenFullThrowsSemaphoreFullException()
        {
            var sem = new UniTaskSemaphore(2, 2);
            Assert.That(() => sem.Release(), Throws.TypeOf<SemaphoreFullException>());
        }

        [Test]
        public void ReleaseDisposedThrowsObjectDisposedException()
        {
            var sem = new UniTaskSemaphore(1, 2);
            sem.Dispose();
            Assert.That(() => sem.Release(), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void ReleaseZeroCountThrows()
        {
            var sem = new UniTaskSemaphore(0, 2);
            Assert.That(() => sem.Release(0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [UnityTest]
        public IEnumerator ReleaseWakesUpWaiters() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 2);
            var result1 = false;
            var result2 = false;

            async UniTask Waiter1() { await sem.WaitAsync(); result1 = true; }
            async UniTask Waiter2() { await sem.WaitAsync(); result2 = true; }

            var task1 = Waiter1();
            var task2 = Waiter2();

            await UniTask.Delay(50);
            Assert.That(result1, Is.False);
            Assert.That(result2, Is.False);

            sem.Release(2);
            await UniTask.WhenAll(task1, task2);
            Assert.That(result1, Is.True);
            Assert.That(result2, Is.True);
        });

        [UnityTest]
        public IEnumerator ReleaseExcessCountThrowsSemaphoreFullException() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 2);
            await sem.WaitAsync(Timeout.Infinite);
            Assert.That(() => sem.Release(3), Throws.TypeOf<SemaphoreFullException>());
        });

        [UnityTest]
        public IEnumerator ReleaseMoreThanWaitersRemainingGoesToCurrentCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 5);
            var waited = false;

            async UniTask WaiterTask() { await sem.WaitAsync(); waited = true; }
            var waitTask = WaiterTask();

            await UniTask.Delay(50);
            sem.Release(2);
            await waitTask;
            Assert.That(waited, Is.True);
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator ReleaseExactlyMatchesWaitersCurrentCountRemainsZero() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 5);
            var count = 0;

            async UniTask Task1() { await sem.WaitAsync(); count++; }
            async UniTask Task2() { await sem.WaitAsync(); count++; }

            var task1 = Task1();
            var task2 = Task2();

            await UniTask.Delay(50);
            sem.Release(2);
            await UniTask.WhenAll(task1, task2);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        });

        [UnityTest]
        public IEnumerator ReleaseReturnsPreviousCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(3, 5);
            await sem.WaitAsync(Timeout.Infinite);
            var prev = sem.Release();
            Assert.That(prev, Is.EqualTo(2u));
            Assert.That(sem.CurrentCount, Is.EqualTo(3u));
        });

        [UnityTest]
        public IEnumerator ReleaseSingleWaiterInList() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var waited = false;

            async UniTask WaiterTask() { await sem.WaitAsync(); waited = true; }
            var waitTask = WaiterTask();

            await UniTask.Delay(50);
            sem.Release();
            await waitTask;
            Assert.That(waited, Is.True);
        });
    }

    #endregion

    #region UniTaskSemaphore Dispose Tests

    public class UniTaskSemaphoreDisposeTests
    {
        [Test]
        public void DisposeSetsDisposed()
        {
            var sem = new UniTaskSemaphore(1, 1);
            sem.Dispose();
            Assert.That(() => sem.WaitAsync(0).Forget(), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void DisposeWithFalseDoesNotSetDisposed()
        {
            var sem = new UniTaskSemaphore(1, 1);
            sem.Dispose(false);
            Assert.That(() => sem.WaitAsync(0).Forget(), Throws.Nothing);
        }

        [Test]
        public void DisposeCalledTwiceNoError()
        {
            var sem = new UniTaskSemaphore(1, 1);
            sem.Dispose();
            Assert.That(() => sem.Dispose(), Throws.Nothing);
        }

        [Test]
        public void DisposeAfterWaitDisposePreventsRelease()
        {
            var sem = new UniTaskSemaphore(1, 2);
            sem.Dispose();
            Assert.That(() => sem.Release(), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void DisposeClearsWaiterList()
        {
            var sem = new UniTaskSemaphore(0, 1);
            sem.Dispose();
            Assert.That(() => sem.WaitAsync(0).Forget(), Throws.TypeOf<ObjectDisposedException>());
        }
    }

    #endregion

    #region UniTaskSemaphore Handle Tests

    public class UniTaskSemaphoreHandleTests
    {
        [UnityTest]
        public IEnumerator WaitScopeAsyncReturnsHandleThatReleasesOnDispose() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));

            using (await sem.WaitScopeAsync())
            {
                Assert.That(sem.CurrentCount, Is.EqualTo(0u));
            }

            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator WaitScopeAsyncWithCancellableTokenReleasesOnDispose() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var cts = new CancellationTokenSource();

            using (await sem.WaitScopeAsync(cts.Token))
            {
                Assert.That(sem.CurrentCount, Is.EqualTo(0u));
            }

            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator WaitScopeAsyncMultipleSequential() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);

            for (var i = 0; i < 3; i++)
            {
                using (await sem.WaitScopeAsync())
                {
                    Assert.That(sem.CurrentCount, Is.EqualTo(0u));
                }
                Assert.That(sem.CurrentCount, Is.EqualTo(1u));
            }
        });

        [UnityTest]
        public IEnumerator WaitScopeAsyncNestedWithDifferentSemaphores() => UniTask.ToCoroutine(async () =>
        {
            var sem1 = new UniTaskSemaphore(1, 1);
            var sem2 = new UniTaskSemaphore(1, 1);

            using (await sem1.WaitScopeAsync())
            {
                Assert.That(sem1.CurrentCount, Is.EqualTo(0u));
                using (await sem2.WaitScopeAsync())
                {
                    Assert.That(sem2.CurrentCount, Is.EqualTo(0u));
                }
                Assert.That(sem2.CurrentCount, Is.EqualTo(1u));
            }
            Assert.That(sem1.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator HandleDisposeReleasesExactlyOnce() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 2);
            var handle = await sem.WaitScopeAsync();
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));

            ((IDisposable)handle).Dispose();
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });
    }

    #endregion

    #region Integration Tests

    public class IntegrationTests
    {
        [UnityTest]
        public IEnumerator SemaphoreConcurrentAccessRespectsLimit() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(2, 2);
            var concurrent = 0;
            var maxConcurrent = 0;

            // スレッドプールを使わずローカルの非同期メソッドでインターリーブさせる
            async UniTask WorkerAsync()
            {
                await sem.WaitAsync();
                concurrent++;
                if (concurrent > maxConcurrent)
                    maxConcurrent = concurrent;

                await UniTask.Delay(30);

                concurrent--;
                sem.Release();
            }

            var tasks = new UniTask[5];
            for (var i = 0; i < 5; i++)
            {
                tasks[i] = WorkerAsync();
            }

            await UniTask.WhenAll(tasks);
            Assert.That(maxConcurrent, Is.LessThanOrEqualTo(2));
            Assert.That(sem.CurrentCount, Is.EqualTo(2u));
        });

        [UnityTest]
        public IEnumerator UniTaskBagWithCompletedTasks() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            bag.Add(UniTask.CompletedTask);
            bag.Add(UniTask.CompletedTask);
            bag.Add(UniTask.CompletedTask);
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator SemaphoreWaitScopeAsyncConcurrentAccess() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var concurrent = 0;
            var maxConcurrent = 0;

            async UniTask WorkerAsync()
            {
                using (await sem.WaitScopeAsync())
                {
                    concurrent++;
                    if (concurrent > maxConcurrent)
                        maxConcurrent = concurrent;

                    await UniTask.Delay(30);
                    concurrent--;
                }
            }

            var tasks = new UniTask[3];
            for (var i = 0; i < 3; i++)
            {
                tasks[i] = WorkerAsync();
            }

            await UniTask.WhenAll(tasks);
            Assert.That(maxConcurrent, Is.LessThanOrEqualTo(1));
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator SemaphoreMultipleWaitersAllReleasedByRelease() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 3);
            var count = 0;

            async UniTask WaiterAsync()
            {
                await sem.WaitAsync();
                count++;
            }

            var task1 = WaiterAsync();
            var task2 = WaiterAsync();
            var task3 = WaiterAsync();

            await UniTask.Delay(50);
            Assert.That(count, Is.EqualTo(0));

            sem.Release(3);
            await UniTask.WhenAll(task1, task2, task3);
            Assert.That(count, Is.EqualTo(3));
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        });

        [UnityTest]
        public IEnumerator SemaphoreReleaseOneByOne() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 2);
            var waited1 = false;
            var waited2 = false;

            async UniTask Task1() { await sem.WaitAsync(); waited1 = true; }
            async UniTask Task2() { await sem.WaitAsync(); waited2 = true; }

            var task1 = Task1();
            var task2 = Task2();

            await UniTask.Delay(50);

            sem.Release();
            await UniTask.Delay(50);

            sem.Release();
            await UniTask.WhenAll(task1, task2);
            Assert.That(waited1, Is.True);
            Assert.That(waited2, Is.True);
        });

        [UnityTest]
        public IEnumerator SemaphoreReleaseWithNoWaitersIncreasesCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 5);
            sem.Release(3);
            Assert.That(sem.CurrentCount, Is.EqualTo(3u));
        });

        [UnityTest]
        public IEnumerator UniTaskBagWithSemaphoreCombinedUsage() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(2, 4);
            var count = 0;

            async UniTask WorkerAsync()
            {
                using (await sem.WaitScopeAsync())
                {
                    await UniTask.Delay(20);
                    count++;
                }
            }

            await using (var bag = new UniTaskBag())
            {
                for (var i = 0; i < 4; i++)
                {
                    bag.Add(WorkerAsync());
                }
            }

            Assert.That(count, Is.EqualTo(4));
            Assert.That(sem.CurrentCount, Is.EqualTo(2u));
        });

        [UnityTest]
        public IEnumerator SemaphoreRapidWaitAndRelease() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            for (var i = 0; i < 10; i++)
            {
                await sem.WaitAsync();
                sem.Release();
            }
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        });

        [UnityTest]
        public IEnumerator SemaphoreSingleWaiterTimeoutReturnsFalse() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(50);
            Assert.That(result, Is.False);
            Assert.That(sem.CurrentCount, Is.EqualTo(0u));
        });
    }

    #endregion
}