using System.Collections.Generic;
using System.Collections.Immutable;

namespace OmniSharp.Utilities
{
    internal static class ImmutableArrayExtensions
    {
        public static ImmutableArray<T> EmptyIfDefault<T>(this ImmutableArray<T> array)
            => array.IsDefault
                ? ImmutableArray<T>.Empty
                : array;

        public static ImmutableArray<T> AsImmutableOrNull<T>(this IEnumerable<T> items)
        {
            if (items == null)
                return default;
 
            return ImmutableArray.CreateRange<T>(items);
        }
    }
}
