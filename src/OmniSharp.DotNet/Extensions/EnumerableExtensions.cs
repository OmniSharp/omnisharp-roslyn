using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.DotNet.Extensions
{
    public static class EnumerableExtensions
    {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> collection)
        {
            return new HashSet<T>(collection);
        }
    }
}
