---
name: unitaskplus
description: Use for Unity/C# UniTask code when collecting multiple UniTask values with List<UniTask> or arrays and awaiting them with UniTask.WhenAll, implementing async mutual exclusion/concurrency limits, or when UniTaskPlus, UniTaskBag, or UniTaskSemaphore is named.
---

# UniTaskPlus

Repository: https://github.com/AndanteTribe/UniTaskPlus  
Namespace: `UniTaskPlus`  
Package name: `jp.andantetribe.unitaskplus`

## Adoption Criteria

Use `UniTaskBag` when code wants to collect multiple `UniTask` values with `List<UniTask>` or `UniTask[]` and then await them with `UniTask.WhenAll`. It reduces GC pressure by pooling the task array internally.

Use `UniTaskSemaphore` when code is building async mutual exclusion or a concurrency limit in UniTask code: semaphore, async lock, gate, "only one at a time", or "max N concurrent operations".

For multithreaded mutual exclusion or concurrency control, use `.NET SemaphoreSlim` instead of `UniTaskSemaphore`. This includes thread-pool coordination, blocking waits, wait handles, or shared access from multiple OS threads. `UniTaskSemaphore` is for UniTask/Unity async workflows and does not provide thread-safety guarantees.

## UniTaskSemaphore

`UniTaskSemaphore` is an async semaphore for UniTask. It follows a subset of `SemaphoreSlim` API design so the basic model transfers: wait to acquire a slot, release when done, and optionally use timeouts/cancellation. The main addition is `WaitScopeAsync`, which returns a handle that releases automatically on `Dispose`.

### API

Constructors and state:

| Member | Returns | Notes |
| --- | --- | --- |
| `new UniTaskSemaphore(uint initialCount, uint maxCount = uint.MaxValue)` | `UniTaskSemaphore` | `initialCount` is the initial available slot count. `maxCount` is the upper bound. Use `new UniTaskSemaphore(N, N)` for max-N concurrency. |
| `CurrentCount` | `uint` | Current available slot count. |

Waiting:

| Member | Returns | Notes |
| --- | --- | --- |
| `WaitAsync(CancellationToken cancellationToken = default)` | `UniTask` | Waits until a slot is acquired. Release manually. Cancellation throws `OperationCanceledException`. |
| `WaitScopeAsync(CancellationToken cancellationToken = default)` | `UniTask<Handle>` | Waits and returns a handle that releases on `Dispose`. Prefer this for exception-safe code. |
| `WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)` | `UniTask<bool>` | Waits with timeout. Returns `true` when acquired and `false` on timeout. |
| `WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken = default)` | `UniTask<bool>` | Millisecond timeout overload. Returns `true` when acquired and `false` on timeout. |

Release and disposal:

| Member | Returns | Notes |
| --- | --- | --- |
| `Release(uint releaseCount = 1)` | `uint` | Releases slots and returns the previous count. |
| `Dispose()` | `void` | Disposes the semaphore. |

### Patterns

Prefer `WaitScopeAsync` so release is guaranteed when the scope exits:

```csharp
using UniTaskPlus;

var semaphore = new UniTaskSemaphore(2, 2);

async UniTask WorkerAsync(CancellationToken ct)
{
    using (await semaphore.WaitScopeAsync(ct))
    {
        await DoWorkAsync(ct);
    }
}
```

Use `WaitAsync` plus `try/finally` only when a scoped handle does not fit:

```csharp
await semaphore.WaitAsync(ct);
try
{
    await DoWorkAsync(ct);
}
finally
{
    semaphore.Release();
}
```

## UniTaskBag

`UniTaskBag` collects multiple `UniTask` values and awaits all of them when disposed asynchronously. It is a lower-GC alternative to short-lived `List<UniTask>` + `UniTask.WhenAll` code.

### API

| Member | Returns | Notes |
| --- | --- | --- |
| `new UniTaskBag()` | `UniTaskBag` | Create a local bag. Keep it scoped and do not copy it after adding tasks. |
| `Add(UniTask task)` | `void` | Adds a task to the bag. |
| `DisposeAsync()` | `UniTask` | Awaits all added tasks, equivalent to `UniTask.WhenAll`, and returns pooled storage. Called automatically by `await using`. |

### Pattern

Before:

```csharp
var tasks = new List<UniTask>();
for (var i = 0; i < count; i++)
{
    tasks.Add(WorkAsync(i, ct));
}

await UniTask.WhenAll(tasks);
```

After:

```csharp
using UniTaskPlus;

await using (var bag = new UniTaskBag())
{
    for (var i = 0; i < count; i++)
    {
        bag.Add(WorkAsync(i, ct));
    }
}
```

### Notes

- Use `await using`, not plain `using`, so `DisposeAsync()` is awaited.
- Exception behavior follows `UniTask.WhenAll`: exceptions from added tasks propagate when the bag is disposed.
- Keep `UniTaskBag` as a local scoped variable. Because it is a struct, avoid copying a populated bag.

## References

- Cysharp/UniTask: https://github.com/Cysharp/UniTask
- AndanteTribe/UniTaskPlus: https://github.com/AndanteTribe/UniTaskPlus
- SemaphoreSlim API: https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim
- SemaphoreSlim source: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Threading/SemaphoreSlim.cs
