#nullable enable

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace OmniSharp.Roslyn.Extensions
{
    public static class OperationExtensions
    {
        public static IEnumerable<IOperation> Descendants(this IOperation? operation, Func<IOperation, bool> descendIntoChildren)
        {
            if (operation == null)
            {
                yield break;
            }

            var stack = new Stack<IEnumerator<IOperation>>();
            stack.Push(operation.Children.GetEnumerator());

            while (stack.Count > 0)
            {
                var enumerator = stack.Pop();

                if (!enumerator.MoveNext())
                {
                    continue;
                }

                var current = enumerator.Current;
                stack.Push(enumerator);

                yield return current;
                if (descendIntoChildren(current))
                {
                    stack.Push(current.Children.GetEnumerator());
                }
            }
        }
    }
}
