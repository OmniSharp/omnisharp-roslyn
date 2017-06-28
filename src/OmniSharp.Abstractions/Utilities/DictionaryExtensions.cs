using System;
using System.Collections.Generic;

namespace OmniSharp.Utilities
{
    internal static class DictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueGetter)
        {
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = valueGetter(key);
                dictionary.Add(key, value);
            }

            return value;
        }
    }
}
