using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace UniTaskPlus.Internal
{
    internal static class ArrayPoolExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Grow<T>(this ArrayPool<T> pool, ref T[] array, int minimumLength)
        {
            if (array.Length < minimumLength)
            {
                var newArray = pool.Rent(minimumLength);
                if (array.Length > 0)
                {
                    var temp = array.AsSpan();
                    temp.CopyTo(newArray);
                    temp.Clear();
                    pool.Return(array);
                }
                array = newArray;
            }
        }
    }
}