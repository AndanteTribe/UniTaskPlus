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

## AI Assistant Skill

UniTaskPlus 1.1.0 and later distribute the `unitask-plus` Agent Skill to assist development with UniTaskPlus. The same Skill can be used through either Unity AI Assistant or the GitHub CLI `gh skill` command.

### Use with Unity AI Assistant

The following versions are required:

- UniTaskPlus 1.1.0 or later
- `com.unity.ai.assistant` 2.8.0-pre.1 or later

When you install UniTaskPlus as a UPM package, the Skill is installed at the following location:

```text
Packages/jp.andantetribe.unitaskplus/AIAssistantSkills/unitask-plus/SKILL.md
```

Supported versions of Unity AI Assistant discover Skills from the `AIAssistantSkills` folder in installed packages. In the Unity Editor's Skills management page, find `unitask-plus` under `Package skills` and set it to `Allow`. If the Skill doesn't appear, rescan the Skill locations from the same page.

For installation and the latest usage instructions, refer to the [latest `com.unity.ai.assistant` documentation](https://docs.unity3d.com/Packages/com.unity.ai.assistant@latest). Older documentation versions might not describe how to use Skills included in UPM packages.

### Use with GitHub CLI

GitHub CLI 2.90.0 and later can install the same Skill for AI agents supported by `gh skill`.

To review the Skill before installation, run:

```shell
gh skill preview AndanteTribe/UniTaskPlus "src/UniTaskPlus.Unity/Packages/jp.andantetribe.unitaskplus/AIAssistantSkills/unitask-plus"
```

To install the Skill, run:

```shell
gh skill install AndanteTribe/UniTaskPlus "src/UniTaskPlus.Unity/Packages/jp.andantetribe.unitaskplus/AIAssistantSkills/unitask-plus"
```

For details about selecting a target AI agent and installation scope, refer to the official [`gh skill install` manual](https://cli.github.com/manual/gh_skill_install).

> `gh skill` is currently a preview feature and its commands and behavior might change.

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
