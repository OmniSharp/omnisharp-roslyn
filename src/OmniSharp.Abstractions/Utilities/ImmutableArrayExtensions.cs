using System.Collections.Immutable;

namespace OmniSharp.Utilities
{
    internal static class ImmutableArrayExtensions
    {
        public static ImmutableArray<T> EmptyIfDefault<T>(this ImmutableArray<T> array)
            => array.IsDefault
                ? ImmutableArray<T>.Empty
                : array;
    }
}
