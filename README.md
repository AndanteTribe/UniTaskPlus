# UniTaskPlus
[![unity-test](https://github.com/AndanteTribe/UniTaskPlus/actions/workflows/unity-test.yml/badge.svg)](https://github.com/AndanteTribe/UniTaskPlus/actions/workflows/unity-test.yml)
[![Releases](https://img.shields.io/github/release/AndanteTribe/UniTaskPlus.svg)](https://github.com/AndanteTribe/UniTaskPlus/releases)
[![GitHub license](https://img.shields.io/github/license/AndanteTribe/UniTaskPlus.svg)](./LICENSE)
[![openupm](https://img.shields.io/npm/v/jp.andantetribe.unitaskplus?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/jp.andantetribe.unitaskplus/)

English | [日本語](README_JA.md)

## Overview
**UniTaskPlus** provides additional utilities and extensions built on top of [UniTask](https://github.com/Cysharp/UniTask).

It includes the following types:

- **`UniTaskSemaphore`** — A `SemaphoreSlim`-like implementation designed for UniTask, supporting async wait with timeout and automatic release via a scoped handle.
- **`UniTaskBag`** — A lightweight struct that collects multiple `UniTask` instances and awaits all of them at once when disposed, similar to `UniTask.WhenAll`.

## Requirements
- Unity 2021.3 or later
- [UniTask](https://github.com/Cysharp/UniTask) 2.5.10 or later

## Installation
Open `Window > Package Manager`, select `[+] > Add package from git URL`, and enter the following URL:

```
https://github.com/AndanteTribe/UniTaskPlus.git?path=src/UniTaskPlus.Unity/Packages/jp.andantetribe.unitaskplus
```

## Quick Start

### UniTaskSemaphore

```csharp
using System;
using Cysharp.Threading.Tasks;
using UniTaskPlus;
using UnityEngine;

public class SemaphoreExample : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        // Semaphore that allows a maximum of 2 concurrent operations
        var semaphore = new UniTaskSemaphore(2, 2);

        // Start 5 workers in parallel, but allow a maximum of 2 concurrent executions
        var tasks = new UniTask[5];
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            tasks[i] = WorkerAsync(idx, semaphore);
        }

        await UniTask.WhenAll(tasks);
    }

    private async UniTask WorkerAsync(int id, UniTaskSemaphore sem)
    {
        // WaitScopeAsync returns an IDisposable handle for release, so it can be automatically released with using
        using (await sem.WaitScopeAsync())
        {
            Debug.Log($"Start {id}");
            await UniTask.Delay(TimeSpan.FromSeconds(1));
            Debug.Log($"End {id}");
        }
    }
}
```

### UniTaskBag

```csharp
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniTaskPlus;
using UnityEngine;

public class UniTaskBagExample : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        // Using a list
        // var list = new List<UniTask>();
        // for (int i = 0; i < 10; i++)
        // {
        //     list.Add(UniTask.Delay(TimeSpan.FromSeconds(i)));
        // }
        // await UniTask.WhenAll(list);

        // Using UniTaskBag (equivalent to the above)
        await using (var bag = new UniTaskBag())
        {
            for (int i = 0; i < 10; i++)
            {
                bag.Add(UniTask.Delay(TimeSpan.FromSeconds(i)));
            }
        } // awaits all added tasks here
    }
}
```

## API

### UniTaskSemaphore

| Member | Description |
|--------|-------------|
| `UniTaskSemaphore(uint initialCount, uint maxCount)` | Initializes a new instance. `initialCount` sets the initial number of allowed concurrent operations; `maxCount` sets the upper limit. |
| `CurrentCount` | Gets the number of remaining slots available. |
| `WaitAsync(CancellationToken)` | Asynchronously waits to enter the semaphore. |
| `WaitScopeAsync(CancellationToken)` | Asynchronously waits to enter the semaphore and returns a `Handle` that releases it on `Dispose`. |
| `WaitAsync(TimeSpan, CancellationToken)` | Asynchronously waits with a `TimeSpan` timeout. Returns `true` if acquired, `false` if timed out. |
| `WaitAsync(int, CancellationToken)` | Asynchronously waits with a millisecond timeout. Returns `true` if acquired, `false` if timed out. |
| `Release(uint releaseCount)` | Releases one or more slots. Returns the count before the release. |
| `Dispose()` | Disposes the semaphore. |

### UniTaskBag

| Member | Description |
|--------|-------------|
| `Add(UniTask task)` | Adds a `UniTask` to the bag. |
| `DisposeAsync()` | Awaits all tasks added to the bag and releases internal resources. Use with `await using`. |

## License
This library is released under the MIT license.
