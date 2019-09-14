using System;
using System.Text;

namespace OmniSharp.Utilities
{
    public struct CachedStringBuilder
    {
        [ThreadStatic]
        private static StringBuilder g_builder;

        public StringBuilder Acquire()
        {
            var builder = g_builder;
            g_builder = null;

            if (builder == null)
            {
                builder = new StringBuilder();
            }

            return builder;
        }

        public void Release(StringBuilder builder)
        {
            builder.Clear();
            if (builder.Capacity > 1024)
            {
                builder.Capacity = 1024;
            }

            g_builder = builder;
        }
    }
}
