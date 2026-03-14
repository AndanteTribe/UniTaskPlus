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
        public void Grow_WhenArrayIsEmpty_RentsNewArray()
        {
            var pool = ArrayPool<int>.Shared;
            int[] array = Array.Empty<int>();

            pool.Grow(ref array, 10);

            Assert.GreaterOrEqual(array.Length, 10);
            pool.Return(array);
        }

        [Test]
        public void Grow_WhenArrayHasContent_CopiesAndClears()
        {
            var pool = ArrayPool<int>.Shared;
            int[] array = pool.Rent(2);
            array[0] = 100;
            array[1] = 200;

            pool.Grow(ref array, 10);

            Assert.AreEqual(100, array[0]);
            Assert.AreEqual(200, array[1]);
            Assert.GreaterOrEqual(array.Length, 10);

            pool.Return(array);
        }
    }

    public class StateTupleTests
    {
        [Test]
        public void StateTuple_1_CreateAndDeconstruct_UsesPool()
        {
            var tuple = StateTuple.Create(1);
            tuple.Deconstruct(out var item1);
            Assert.AreEqual(1, item1);

            var tuple2 = StateTuple.Create(2);
            tuple2.Deconstruct(out var item2);
            Assert.AreEqual(2, item2);
        }

        [Test]
        public void StateTuple_2_CreateAndDeconstruct_UsesPool()
        {
            var tuple = StateTuple.Create(1, "A");
            tuple.Deconstruct(out var i1, out var s1);
            Assert.AreEqual(1, i1);
            Assert.AreEqual("A", s1);

            var tuple2 = StateTuple.Create(2, "B");
            tuple2.Deconstruct(out var i2, out var s2);
            Assert.AreEqual(2, i2);
            Assert.AreEqual("B", s2);
        }

        [Test]
        public void StateTuple_3_CreateAndDeconstruct_UsesPool()
        {
            var tuple = StateTuple.Create(1, "A", 0.5f);
            tuple.Deconstruct(out var i1, out var s1, out var f1);
            Assert.AreEqual(1, i1);
            Assert.AreEqual("A", s1);
            Assert.AreEqual(0.5f, f1);

            var tuple2 = StateTuple.Create(2, "B", 1.5f);
            tuple2.Deconstruct(out var i2, out var s2, out var f2);
            Assert.AreEqual(2, i2);
            Assert.AreEqual("B", s2);
            Assert.AreEqual(1.5f, f2);
        }
    }

    public class UniTaskNodeTests
    {
        [UnityTest]
        public IEnumerator Node_TrySetException_And_Status() => UniTask.ToCoroutine(async () =>
        {
            var node = UniTaskNode<bool>.Create();
            var task = node.WaitAsync(Timeout.Infinite);

            Assert.AreEqual(UniTaskStatus.Pending, node.UnsafeGetStatus());

            node.TrySetException(new InvalidOperationException());

            // awaitで消費される前にステータスを確認
            Assert.AreEqual(UniTaskStatus.Faulted, node.UnsafeGetStatus());

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
        public IEnumerator Node_Timeout_Coverage() => UniTask.ToCoroutine(async () =>
        {
            var node = UniTaskNode<bool>.Create();
            var task = node.WaitAsync(10);

            // タイムアウトを待機
            await UniTask.Delay(50);

            // awaitで消費される前にステータスを確認
            Assert.AreEqual(UniTaskStatus.Canceled, node.UnsafeGetStatus());

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
        public IEnumerator Node_ExternalCancellation_Coverage() => UniTask.ToCoroutine(async () =>
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
        public IEnumerator Node_Pooling_And_Properties() => UniTask.ToCoroutine(async () =>
        {
            var node1 = UniTaskNode<int>.Create();
            node1.Next = null;
            node1.Prev = null;

            var task1 = node1.WaitAsync(Timeout.Infinite);
            node1.TrySetResult(42);

            var result = await task1;
            Assert.AreEqual(42, result);

            var node2 = UniTaskNode<int>.Create();
            var task2 = node2.WaitAsync(Timeout.Infinite);
            node2.TrySetResult(99);

            var result2 = await task2;
            Assert.AreEqual(99, result2);

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
        public IEnumerator DisposeAsync_WhenEmpty_ReturnsCompletedTask() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator Add_SingleTask_CompletesOnDispose() => UniTask.ToCoroutine(async () =>
        {
            var completed = false;
            var bag = new UniTaskBag();
            bag.Add(UniTask.RunOnThreadPool(() => completed = true));
            await bag.DisposeAsync();
            Assert.IsTrue(completed);
        });

        [UnityTest]
        public IEnumerator Add_TwoTasks_TriggersGrow() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            var bag = new UniTaskBag();
            bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
            bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
            await bag.DisposeAsync();
            Assert.AreEqual(2, count);
        });

        [UnityTest]
        public IEnumerator Add_MultipleTasks_AllCompleteOnDispose() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            var bag = new UniTaskBag();
            for (var i = 0; i < 5; i++)
            {
                bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
            }
            await bag.DisposeAsync();
            Assert.AreEqual(5, count);
        });

        [UnityTest]
        public IEnumerator Add_ManyTasks_GrowsMultipleTimes() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            var bag = new UniTaskBag();
            for (var i = 0; i < 20; i++)
            {
                bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
            }
            await bag.DisposeAsync();
            Assert.AreEqual(20, count);
        });

        [UnityTest]
        public IEnumerator AwaitUsing_Pattern() => UniTask.ToCoroutine(async () =>
        {
            var completed = false;
            await using (var bag = new UniTaskBag())
            {
                bag.Add(UniTask.RunOnThreadPool(() => completed = true));
            }
            Assert.IsTrue(completed);
        });

        [UnityTest]
        public IEnumerator DisposeAsync_CalledTwice_NoError() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            bag.Add(UniTask.CompletedTask);
            await bag.DisposeAsync();
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator Add_DelayedTasks_AllCompleteOnDispose() => UniTask.ToCoroutine(async () =>
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
            Assert.AreEqual(3, count);
        });

        [UnityTest]
        public IEnumerator Add_LargeNumberOfCompletedTasks() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            for (var i = 0; i < 100; i++)
            {
                bag.Add(UniTask.CompletedTask);
            }
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator AwaitUsing_MultipleTasks_Pattern() => UniTask.ToCoroutine(async () =>
        {
            var count = 0;
            await using (var bag = new UniTaskBag())
            {
                for (var i = 0; i < 10; i++)
                {
                    bag.Add(UniTask.RunOnThreadPool(() => Interlocked.Increment(ref count)));
                }
            }
            Assert.AreEqual(10, count);
        });

        [UnityTest]
        public IEnumerator DisposeAsync_WhenNoTasksAdded_ReturnsImmediately() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator Add_ExactlyOneTask_UsesRentPath() => UniTask.ToCoroutine(async () =>
        {
            var completed = false;
            var bag = new UniTaskBag();
            bag.Add(UniTask.RunOnThreadPool(() => completed = true));
            await bag.DisposeAsync();
            Assert.IsTrue(completed);
        });
    }

    #endregion

    #region UniTaskSemaphore Constructor Tests

    public class UniTaskSemaphoreConstructorTests
    {
        [Test]
        public void Constructor_ValidParameters_SetCurrentCount()
        {
            var sem = new UniTaskSemaphore(3, 5);
            Assert.AreEqual(3u, sem.CurrentCount);
        }

        [Test]
        public void Constructor_DefaultMaxCount_SetCurrentCount()
        {
            var sem = new UniTaskSemaphore(1);
            Assert.AreEqual(1u, sem.CurrentCount);
        }

        [Test]
        public void Constructor_InitialCountGreaterThanMaxCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UniTaskSemaphore(5, 3));
        }

        [Test]
        public void Constructor_MaxCountZero_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UniTaskSemaphore(0, 0));
        }

        [Test]
        public void Constructor_ZeroInitialCount_Valid()
        {
            var sem = new UniTaskSemaphore(0, 5);
            Assert.AreEqual(0u, sem.CurrentCount);
        }

        [Test]
        public void Constructor_InitialEqualsMaxCount_Valid()
        {
            var sem = new UniTaskSemaphore(10, 10);
            Assert.AreEqual(10u, sem.CurrentCount);
        }

        [Test]
        public void Constructor_DefaultMaxCount_AllowsRelease()
        {
            var sem = new UniTaskSemaphore(0);
            Assert.AreEqual(0u, sem.CurrentCount);
            sem.Release();
            Assert.AreEqual(1u, sem.CurrentCount);
        }

        [Test]
        public void Constructor_LargeMaxCount_Valid()
        {
            var sem = new UniTaskSemaphore(100, 1000);
            Assert.AreEqual(100u, sem.CurrentCount);
        }
    }

    #endregion

    #region UniTaskSemaphore WaitAsync Tests

    public class UniTaskSemaphoreWaitAsyncTests
    {
        [UnityTest]
        public IEnumerator WaitAsync_CountAvailable_DecrementsAndReturnsTrue() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(2, 2);
            var result = await sem.WaitAsync(Timeout.Infinite);
            Assert.IsTrue(result);
            Assert.AreEqual(1u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitAsync_ZeroTimeout_WhenNoCount_ReturnsFalse() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(0);
            Assert.IsFalse(result);
        });

        [UnityTest]
        public IEnumerator WaitAsync_Disposed_ThrowsObjectDisposedException() => UniTask.ToCoroutine(async () =>
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
        public void WaitAsync_NegativeTimeout_Throws()
        {
            var sem = new UniTaskSemaphore(1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => sem.WaitAsync(-2).Forget());
        }

        [UnityTest]
        public IEnumerator WaitAsync_CancellationAlreadyRequested_ReturnsCanceled() => UniTask.ToCoroutine(async () =>
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
        public IEnumerator WaitAsync_WithTimeSpan_Valid() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var result = await sem.WaitAsync(TimeSpan.FromMilliseconds(1000));
            Assert.IsTrue(result);
        });

        [Test]
        public void WaitAsync_WithTimeSpan_InvalidRange_Throws()
        {
            var sem = new UniTaskSemaphore(1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                sem.WaitAsync(TimeSpan.FromMilliseconds(-2)).Forget());
        }

        [Test]
        public void WaitAsync_WithTimeSpan_TooLarge_Throws()
        {
            var sem = new UniTaskSemaphore(1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                sem.WaitAsync(TimeSpan.FromMilliseconds((double)int.MaxValue + 1)).Forget());
        }

        [UnityTest]
        public IEnumerator WaitAsync_InfiniteTimeout_NoCancellation_DirectWaitPath() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var waitTask = sem.WaitAsync(Timeout.Infinite);
            await UniTask.Delay(50);
            sem.Release();
            await waitTask;
            Assert.AreEqual(0u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitAsync_InfiniteTimeout_WithCancellableToken() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var cts = new CancellationTokenSource();
            var waitTask = sem.WaitAsync(Timeout.Infinite, cts.Token);
            await UniTask.Delay(50);
            sem.Release();
            var result = await waitTask;
            Assert.IsTrue(result);
        });

        [UnityTest]
        public IEnumerator WaitAsync_FiniteTimeout_Released_ReturnsTrue() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var waitTask = sem.WaitAsync(5000);
            await UniTask.Delay(50);
            sem.Release();
            var result = await waitTask;
            Assert.IsTrue(result);
        });

        [UnityTest]
        public IEnumerator WaitAsync_FiniteTimeout_TimesOut_ReturnsFalse() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(50);
            Assert.IsFalse(result);
            Assert.AreEqual(0u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitAsync_CancellationWhileWaiting_ThrowsCancellation() => UniTask.ToCoroutine(async () =>
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
        public IEnumerator WaitAsync_Default_NoParameters() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            await sem.WaitAsync();
            Assert.AreEqual(0u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitAsync_NoCancel_InfiniteTimeout_WaitsAndCompletes() => UniTask.ToCoroutine(async () =>
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
            Assert.IsFalse(waited);
            sem.Release();
            await waitTask;
            Assert.IsTrue(waited);
        });

        [UnityTest]
        public IEnumerator WaitAsync_WithCancellableToken_ButCompletes() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var cts = new CancellationTokenSource();
            var waitTask = sem.WaitAsync(Timeout.Infinite, cts.Token);
            await UniTask.Delay(50);
            sem.Release();
            var result = await waitTask;
            Assert.IsTrue(result);
        });

        [UnityTest]
        public IEnumerator WaitAsync_WithTimeSpan_InfiniteTimeout_Valid() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var result = await sem.WaitAsync(TimeSpan.FromMilliseconds(-1));
            Assert.IsTrue(result);
            Assert.AreEqual(0u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitAsync_WithTimeSpan_ZeroTimeout_CountAvailable() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var result = await sem.WaitAsync(TimeSpan.Zero);
            Assert.IsTrue(result);
        });

        [UnityTest]
        public IEnumerator WaitAsync_WithTimeSpan_ZeroTimeout_NoCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(TimeSpan.Zero);
            Assert.IsFalse(result);
        });

        [UnityTest]
        public IEnumerator WaitAsync_MultipleDecrementsToZero() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(3, 3);
            await sem.WaitAsync(Timeout.Infinite);
            await sem.WaitAsync(Timeout.Infinite);
            await sem.WaitAsync(Timeout.Infinite);
            Assert.AreEqual(0u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitAsync_Disposed_WithCancellationToken_Throws() => UniTask.ToCoroutine(async () =>
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
        public IEnumerator WaitAsync_CountAvailable_WithCancellableToken() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(2, 2);
            var cts = new CancellationTokenSource();
            var result = await sem.WaitAsync(Timeout.Infinite, cts.Token);
            Assert.IsTrue(result);
            Assert.AreEqual(1u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitAsync_Timeout_ReturnsFalse() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(50);
            Assert.IsFalse(result);
        });
    }

    #endregion

    #region UniTaskSemaphore Release Tests

    public class UniTaskSemaphoreReleaseTests
    {
        [UnityTest]
        public IEnumerator Release_IncrementsCurrentCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 2);
            await sem.WaitAsync(Timeout.Infinite);
            Assert.AreEqual(0u, sem.CurrentCount);
            var prev = sem.Release();
            Assert.AreEqual(0u, prev);
            Assert.AreEqual(1u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Release_MultipleSlots() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(3, 5);
            await sem.WaitAsync(Timeout.Infinite);
            await sem.WaitAsync(Timeout.Infinite);
            await sem.WaitAsync(Timeout.Infinite);
            Assert.AreEqual(0u, sem.CurrentCount);
            var prev = sem.Release(3);
            Assert.AreEqual(0u, prev);
            Assert.AreEqual(3u, sem.CurrentCount);
        });

        [Test]
        public void Release_WhenFull_ThrowsSemaphoreFullException()
        {
            var sem = new UniTaskSemaphore(2, 2);
            Assert.Throws<SemaphoreFullException>(() => sem.Release());
        }

        [Test]
        public void Release_Disposed_ThrowsObjectDisposedException()
        {
            var sem = new UniTaskSemaphore(1, 2);
            sem.Dispose();
            Assert.Throws<ObjectDisposedException>(() => sem.Release());
        }

        [Test]
        public void Release_ZeroCount_Throws()
        {
            var sem = new UniTaskSemaphore(0, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => sem.Release(0));
        }

        [UnityTest]
        public IEnumerator Release_WakesUpWaiters() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 2);
            var result1 = false;
            var result2 = false;

            async UniTask Waiter1() { await sem.WaitAsync(); result1 = true; }
            async UniTask Waiter2() { await sem.WaitAsync(); result2 = true; }

            var task1 = Waiter1();
            var task2 = Waiter2();

            await UniTask.Delay(50);
            Assert.IsFalse(result1);
            Assert.IsFalse(result2);

            sem.Release(2);
            await UniTask.WhenAll(task1, task2);
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
        });

        [UnityTest]
        public IEnumerator Release_ExcessCount_ThrowsSemaphoreFullException() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 2);
            await sem.WaitAsync(Timeout.Infinite);
            Assert.Throws<SemaphoreFullException>(() => sem.Release(3));
        });

        [UnityTest]
        public IEnumerator Release_MoreThanWaiters_RemainingGoesToCurrentCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 5);
            var waited = false;

            async UniTask WaiterTask() { await sem.WaitAsync(); waited = true; }
            var waitTask = WaiterTask();

            await UniTask.Delay(50);
            sem.Release(2);
            await waitTask;
            Assert.IsTrue(waited);
            Assert.AreEqual(1u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Release_ExactlyMatchesWaiters_CurrentCountRemainsZero() => UniTask.ToCoroutine(async () =>
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
            Assert.AreEqual(2, count);
            Assert.AreEqual(0u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Release_ReturnsPreviousCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(3, 5);
            await sem.WaitAsync(Timeout.Infinite);
            var prev = sem.Release();
            Assert.AreEqual(2u, prev);
            Assert.AreEqual(3u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Release_SingleWaiter_InList() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var waited = false;

            async UniTask WaiterTask() { await sem.WaitAsync(); waited = true; }
            var waitTask = WaiterTask();

            await UniTask.Delay(50);
            sem.Release();
            await waitTask;
            Assert.IsTrue(waited);
        });
    }

    #endregion

    #region UniTaskSemaphore Dispose Tests

    public class UniTaskSemaphoreDisposeTests
    {
        [Test]
        public void Dispose_SetsDisposed()
        {
            var sem = new UniTaskSemaphore(1, 1);
            sem.Dispose();
            Assert.Throws<ObjectDisposedException>(() => sem.WaitAsync(0).Forget());
        }

        [Test]
        public void Dispose_WithFalse_DoesNotSetDisposed()
        {
            var sem = new UniTaskSemaphore(1, 1);
            sem.Dispose(false);
            Assert.DoesNotThrow(() => sem.WaitAsync(0).Forget());
        }

        [Test]
        public void Dispose_CalledTwice_NoError()
        {
            var sem = new UniTaskSemaphore(1, 1);
            sem.Dispose();
            Assert.DoesNotThrow(() => sem.Dispose());
        }

        [Test]
        public void Dispose_AfterWait_DisposePreventsRelease()
        {
            var sem = new UniTaskSemaphore(1, 2);
            sem.Dispose();
            Assert.Throws<ObjectDisposedException>(() => sem.Release());
        }

        [Test]
        public void Dispose_ClearsWaiterList()
        {
            var sem = new UniTaskSemaphore(0, 1);
            sem.Dispose();
            Assert.Throws<ObjectDisposedException>(() => sem.WaitAsync(0).Forget());
        }
    }

    #endregion

    #region UniTaskSemaphore Handle Tests

    public class UniTaskSemaphoreHandleTests
    {
        [UnityTest]
        public IEnumerator WaitScopeAsync_ReturnsHandle_ThatReleasesOnDispose() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            Assert.AreEqual(1u, sem.CurrentCount);

            using (await sem.WaitScopeAsync())
            {
                Assert.AreEqual(0u, sem.CurrentCount);
            }

            Assert.AreEqual(1u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitScopeAsync_WithCancellation() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            var cts = new CancellationTokenSource();

            using (await sem.WaitScopeAsync(cts.Token))
            {
                Assert.AreEqual(0u, sem.CurrentCount);
            }

            Assert.AreEqual(1u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator WaitScopeAsync_MultipleSequential() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);

            for (var i = 0; i < 3; i++)
            {
                using (await sem.WaitScopeAsync())
                {
                    Assert.AreEqual(0u, sem.CurrentCount);
                }
                Assert.AreEqual(1u, sem.CurrentCount);
            }
        });

        [UnityTest]
        public IEnumerator WaitScopeAsync_NestedWithDifferentSemaphores() => UniTask.ToCoroutine(async () =>
        {
            var sem1 = new UniTaskSemaphore(1, 1);
            var sem2 = new UniTaskSemaphore(1, 1);

            using (await sem1.WaitScopeAsync())
            {
                Assert.AreEqual(0u, sem1.CurrentCount);
                using (await sem2.WaitScopeAsync())
                {
                    Assert.AreEqual(0u, sem2.CurrentCount);
                }
                Assert.AreEqual(1u, sem2.CurrentCount);
            }
            Assert.AreEqual(1u, sem1.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Handle_Dispose_ReleasesExactlyOnce() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 2);
            var handle = await sem.WaitScopeAsync();
            Assert.AreEqual(0u, sem.CurrentCount);

            ((IDisposable)handle).Dispose();
            Assert.AreEqual(1u, sem.CurrentCount);
        });
    }

    #endregion

    #region Integration Tests

    public class IntegrationTests
    {
        [UnityTest]
        public IEnumerator Semaphore_ConcurrentAccess_RespectsLimit() => UniTask.ToCoroutine(async () =>
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
            Assert.LessOrEqual(maxConcurrent, 2);
            Assert.AreEqual(2u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator UniTaskBag_WithCompletedTasks() => UniTask.ToCoroutine(async () =>
        {
            var bag = new UniTaskBag();
            bag.Add(UniTask.CompletedTask);
            bag.Add(UniTask.CompletedTask);
            bag.Add(UniTask.CompletedTask);
            await bag.DisposeAsync();
        });

        [UnityTest]
        public IEnumerator Semaphore_WaitScopeAsync_ConcurrentAccess() => UniTask.ToCoroutine(async () =>
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
            Assert.LessOrEqual(maxConcurrent, 1);
            Assert.AreEqual(1u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Semaphore_MultipleWaiters_AllReleasedByRelease() => UniTask.ToCoroutine(async () =>
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
            Assert.AreEqual(0, count);

            sem.Release(3);
            await UniTask.WhenAll(task1, task2, task3);
            Assert.AreEqual(3, count);
            Assert.AreEqual(0u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Semaphore_ReleaseOneByOne() => UniTask.ToCoroutine(async () =>
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
            Assert.IsTrue(waited1);
            Assert.IsTrue(waited2);
        });

        [UnityTest]
        public IEnumerator Semaphore_ReleaseWithNoWaiters_IncreasesCount() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 5);
            sem.Release(3);
            Assert.AreEqual(3u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator UniTaskBag_WithSemaphore_CombinedUsage() => UniTask.ToCoroutine(async () =>
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

            Assert.AreEqual(4, count);
            Assert.AreEqual(2u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Semaphore_RapidWaitAndRelease() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(1, 1);
            for (var i = 0; i < 10; i++)
            {
                await sem.WaitAsync();
                sem.Release();
            }
            Assert.AreEqual(1u, sem.CurrentCount);
        });

        [UnityTest]
        public IEnumerator Semaphore_SingleWaiterTimeout_ReturnsFalse() => UniTask.ToCoroutine(async () =>
        {
            var sem = new UniTaskSemaphore(0, 1);
            var result = await sem.WaitAsync(50);
            Assert.IsFalse(result);
            Assert.AreEqual(0u, sem.CurrentCount);
        });
    }

    #endregion
}