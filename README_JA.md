# UniTaskPlus
[![unity-test](https://github.com/AndanteTribe/UniTaskPlus/actions/workflows/unity-test.yml/badge.svg)](https://github.com/AndanteTribe/UniTaskPlus/actions/workflows/unity-test.yml)
[![Releases](https://img.shields.io/github/release/AndanteTribe/UniTaskPlus.svg)](https://github.com/AndanteTribe/UniTaskPlus/releases)
[![GitHub license](https://img.shields.io/github/license/AndanteTribe/UniTaskPlus.svg)](./LICENSE)
[![openupm](https://img.shields.io/npm/v/jp.andantetribe.unitaskplus?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/jp.andantetribe.unitaskplus/)

[English](README.md) | 日本語

## 概要
**UniTaskPlus** は、[UniTask](https://github.com/Cysharp/UniTask) を基盤とした追加のユーティリティおよび拡張機能を提供します。

以下の型が含まれています。

- **`UniTaskSemaphore`** — UniTask 向けに設計された `SemaphoreSlim` ライクな実装です。タイムアウト付きの非同期待機と、スコープハンドルによる自動解放をサポートします。
- **`UniTaskBag`** — 複数の `UniTask` を収集し、破棄時に `UniTask.WhenAll` のようにすべてをまとめて待機する軽量な構造体です。

## 要件
- Unity 2021.3 以上
- [UniTask](https://github.com/Cysharp/UniTask) 2.5.10 以上

## インストール
`Window > Package Manager` から Package Manager ウィンドウを開き、`[+] > Add package from git URL` を選択して以下の URL を入力します。

```
https://github.com/AndanteTribe/UniTaskPlus.git?path=src/UniTaskPlus.Unity/Packages/jp.andantetribe.unitaskplus
```

## クイックスタート

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
        // 最大2つの同時実行を許可するセマフォ
        var semaphore = new UniTaskSemaphore(2, 2);

        // 5つのワーカーを並列起動するが、同時実行は最大2つに制限
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
        // WaitScopeAsync は解放用の IDisposable ハンドルを返すため、using で自動解放できる
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
        // List を使う場合
        // var list = new List<UniTask>();
        // for (int i = 0; i < 10; i++)
        // {
        //     list.Add(UniTask.Delay(TimeSpan.FromSeconds(i)));
        // }
        // await UniTask.WhenAll(list);

        // UniTaskBag を使う場合（上記と同等）
        await using (var bag = new UniTaskBag())
        {
            for (int i = 0; i < 10; i++)
            {
                bag.Add(UniTask.Delay(TimeSpan.FromSeconds(i)));
            }
        } // ここで追加されたすべてのタスクを待機する
    }
}
```

## API

### UniTaskSemaphore

| メンバー | 説明 |
|--------|------|
| `UniTaskSemaphore(uint initialCount, uint maxCount)` | 新しいインスタンスを初期化します。`initialCount` で初期の同時実行許可数、`maxCount` で上限を設定します。 |
| `CurrentCount` | 現在の残り利用可能スロット数を取得します。 |
| `WaitAsync(CancellationToken)` | セマフォへの非同期待機を行います。 |
| `WaitScopeAsync(CancellationToken)` | セマフォへの非同期待機を行い、`Dispose` 時に解放される `Handle` を返します。 |
| `WaitAsync(TimeSpan, CancellationToken)` | `TimeSpan` タイムアウト付きで非同期待機します。取得できた場合 `true`、タイムアウトした場合 `false` を返します。 |
| `WaitAsync(int, CancellationToken)` | ミリ秒タイムアウト付きで非同期待機します。取得できた場合 `true`、タイムアウトした場合 `false` を返します。 |
| `Release(uint releaseCount)` | 1つ以上のスロットを解放します。解放前のカウントを返します。 |
| `Dispose()` | セマフォを破棄します。 |

### UniTaskBag

| メンバー | 説明 |
|--------|------|
| `Add(UniTask task)` | バッグに `UniTask` を追加します。 |
| `DisposeAsync()` | バッグに追加されたすべてのタスクを待機し、内部リソースを解放します。`await using` と組み合わせて使用してください。 |

## ライセンス
このライブラリは、MIT ライセンスで公開しています。
