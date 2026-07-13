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

## AI Assistant Skill

UniTaskPlus 1.1.0 以降では、UniTaskPlus を利用した開発を支援する Agent Skill `unitask-plus` を配布しています。同じ Skill を、Unity AI Assistant と GitHub CLI の `gh skill` の2つの方法で利用できます。

### Unity AI Assistant で利用する

以下のバージョンが必要です。

- UniTaskPlus 1.1.0 以上
- `com.unity.ai.assistant` 2.8.0-pre.1 以上

UniTaskPlus を UPM パッケージとしてインストールすると、Skill は次の場所に配置されます。

```text
Packages/jp.andantetribe.unitaskplus/AIAssistantSkills/unitask-plus/SKILL.md
```

対応バージョンの Unity AI Assistant は、インストール済みパッケージの `AIAssistantSkills` フォルダーから Skill を検出します。Unity Editor の Skills 管理画面で `Package skills` に表示される `unitask-plus` を `Allow` に設定してください。Skill が表示されない場合は、同じ画面から再スキャンを実行してください。

Unity AI Assistant の導入方法や最新の操作手順については、[`com.unity.ai.assistant` の最新ドキュメント](https://docs.unity3d.com/Packages/com.unity.ai.assistant@latest)を参照してください。古いバージョンのドキュメントには、UPM パッケージに含まれる Skill の利用方法が掲載されていない場合があります。

### GitHub CLI で利用する

GitHub CLI 2.90.0 以降では、`gh skill` が対応する AI エージェントに同じ Skill をインストールできます。

インストール前に Skill の内容を確認するには、次のコマンドを実行します。

```shell
gh skill preview AndanteTribe/UniTaskPlus "src/UniTaskPlus.Unity/Packages/jp.andantetribe.unitaskplus/AIAssistantSkills/unitask-plus"
```

Skill をインストールするには、次のコマンドを実行します。

```shell
gh skill install AndanteTribe/UniTaskPlus "src/UniTaskPlus.Unity/Packages/jp.andantetribe.unitaskplus/AIAssistantSkills/unitask-plus"
```

インストール先の AI エージェントやスコープの指定方法については、[`gh skill install` の公式マニュアル](https://cli.github.com/manual/gh_skill_install)を参照してください。

> `gh skill` は現在プレビュー機能であり、今後コマンドや動作が変更される可能性があります。

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
