using System;

namespace OmniSharp.Utilities
{
    public static class Functional
    {
        public static TReturn Apply<T, TReturn>(T value, Func<T, TReturn> target)
        {
            return target(value);
        }
    }
}
