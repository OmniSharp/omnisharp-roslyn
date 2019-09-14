using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using OmniSharp.Eventing;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TestEventEmitter<T> : IEventEmitter
        {
            public ImmutableArray<T> Messages { get; private set; } = ImmutableArray<T>.Empty;

            public async Task ExpectForEmitted(Expression<Predicate<T>> predicate)
            {
                var asCompiledPredicate = predicate.Compile();

                // May seem hacky but nothing is more painfull to debug than infinite hanging test ...
                for(int i = 0; i < 100; i++)
                {
                    if(Messages.Any(m => asCompiledPredicate(m)))
                    {
                        return;
                    }

                    await Task.Delay(250);
                }

                throw new InvalidOperationException($"Timeout reached before expected event count reached before prediction {predicate} came true, current diagnostics '{String.Join(";", Messages)}'");
            }

            public void Clear()
            {
                Messages = ImmutableArray<T>.Empty;
            }

            public void Emit(string kind, object args)
            {
                if(args is T asT)
                {
                    Messages = Messages.Add(asT);
                }
            }
        }
}