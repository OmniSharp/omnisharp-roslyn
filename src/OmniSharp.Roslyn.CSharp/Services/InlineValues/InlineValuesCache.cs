#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.v1.InlineValues;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OmniSharp.Roslyn.CSharp.Services.InlineValues
{
    /// <summary>
    /// Caches inline value results between calls to <see cref="InlineValuesService"/> for documents
    /// with the same id.
    /// </summary>
    internal sealed class InlineValuesCache
    {
        /// <summary>
        /// Number of simultaneous documents to maintain a cache for.
        /// </summary>
        private const int MaxDocumentsCached = 10;
        /// <summary>
        /// Number of simultaneous methods in a document to maintain a cache for.
        /// </summary>
        private const int MaxMethodsCached = 10;

        /// <summary>
        /// Protects access to the cache elements.
        /// </summary>
        private readonly object _lock = new();

        #region protected by _lock
        /// <summary>
        /// The actual cache. Elements are maintained in a recently-accessed order:
        ///     When a document is accessed, it is removed from the current location and added to the end.
        ///     When the cached is filled and a new item needs to be added, the first element of the list is removed.
        /// The inner list of cached locations in a document is maintained in similar fashion.
        /// </summary>
        private List<(DocumentId DocumentId, List<(TextSpan MemberSpan, List<InlineValue> Values)>)> _cache = new(MaxDocumentsCached);
        #endregion

        private static bool TryGetCachedValue<TKey, TValue>(List<(TKey, TValue)> list, [DisallowNull] TKey expectedKey, [MaybeNullWhen(false)] out TValue result)
            where TKey : IEquatable<TKey>
        {
            // Go from most-recently access to least.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var (key, value) = list[i];
                if (expectedKey.Equals(key))
                {
                    // If this entry isn't already the most-recently access element,
                    // remove from the current location and put it at the end
                    if (i + 1 != list.Count)
                    {
                        list.RemoveAt(i);
                        list.Add((key, value));
                    }

                    result = value;
                    return true;
                }
            }

            result = default;
            return false;
        }

        public List<InlineValue>? TryGetCachedValues(DocumentId documentId, TextSpan memberSpan)
        {
            lock (_lock)
            {
                if (!TryGetCachedValue(_cache, documentId, out var memberCache))
                {
                    return null;
                }

                if (!TryGetCachedValue(memberCache, memberSpan, out var values))
                {
                    return null;
                }

                return values;
            }
        }

        public void CacheResults(DocumentId documentId, TextSpan memberSpan, List<InlineValue> values)
        {
            lock (_lock)
            {
                if (!TryGetCachedValue(_cache, documentId, out var memberCache))
                {
                    // Not in the cache, add a new entry, trimming if necessary.
                    memberCache = new();
                    AddToCache(_cache, (documentId, memberCache), MaxDocumentsCached);
                }

                // Did someone else add to the cache?
                if (TryGetCachedValue(memberCache, memberSpan, out var cachedValues))
                {
                    Debug.Assert(values.SequenceEqual(cachedValues));
                    return;
                }

                AddToCache(memberCache, (memberSpan, values), MaxMethodsCached);
            }

            static void AddToCache<T>(List<T> cache, T toAdd, int maxCache)
            {
                if (cache.Count > maxCache)
                {
                    cache.RemoveAt(0);
                }

                cache.Add(toAdd);
            }
        }
    }
}
