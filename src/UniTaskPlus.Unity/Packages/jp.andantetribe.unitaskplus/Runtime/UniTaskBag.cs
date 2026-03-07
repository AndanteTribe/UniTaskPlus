#nullable enable

using System;
using System.Buffers;
using Cysharp.Threading.Tasks;
using UniTaskPlus.Internal;

namespace UniTaskPlus
{
    /// <summary>
    /// A bag that collects multiple <see cref="UniTask"/> and completes when all of them are completed.
    /// </summary>
    /// <remarks>
    /// The behavior of concurrently waiting for multiple <see cref="UniTask"/> is the same as <see cref="UniTask.WhenAll(UniTask[])"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// using System;
    /// using Cysharp.Threading.Tasks;
    /// using UniTaskPlus;
    /// using UnityEngine;
    ///
    /// public class Example : MonoBehaviour
    /// {
    ///     private async UniTaskVoid Start()
    ///     {
    ///         // Using a list
    ///         // var list = new List<UniTask>();
    ///         // for (int i = 0; i < 10; i++)
    ///         // {
    ///         //     list.Add(UniTask.Delay(TimeSpan.FromSeconds(i)));
    ///         // }
    ///         // await UniTask.WhenAll(list);
    ///
    ///         // Using UniTaskBag
    ///         await using (var bag = new UniTaskBag())
    ///         {
    ///             for (int i = 0; i < 10; i++)
    ///             {
    ///                 bag.Add(UniTask.Delay(TimeSpan.FromSeconds(i)));
    ///             }
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public struct UniTaskBag : IUniTaskAsyncDisposable
    {
        private UniTask[]? _tasks;
        private int _count;

        /// <summary>
        /// Adds a <see cref="UniTask"/> to the bag.
        /// </summary>
        /// <param name="task">The <see cref="UniTask"/> to add.</param>
        public void Add(UniTask task)
        {
            if (_tasks == null)
            {
                _tasks = ArrayPool<UniTask>.Shared.Rent(1);
            }
            else
            {
                ArrayPool<UniTask>.Shared.Grow(ref _tasks, _count + 1);
            }

            _tasks[_count++] = task;
        }

        /// <inheritdoc />
        public UniTask DisposeAsync()
        {
            if (_tasks == null)
            {
                return UniTask.CompletedTask;
            }

            try
            {
                return UniTask.WhenAll(_tasks);
            }
            finally
            {
                ArrayPool<UniTask>.Shared.Return(_tasks, true);
            }
        }
    }
}
