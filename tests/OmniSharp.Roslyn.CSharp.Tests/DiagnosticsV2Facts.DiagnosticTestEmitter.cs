using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using OmniSharp.Eventing;
using OmniSharp.Models.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public partial class DiagnosticsV2Facts
    {
        private class DiagnosticTestEmitter : IEventEmitter
        {
            public readonly ConcurrentBag<DiagnosticMessage> Messages = new ConcurrentBag<DiagnosticMessage>();

            private readonly TaskCompletionSource<object> _tcs;

            public async Task ExpectForEmitted(Expression<Predicate<DiagnosticMessage>> predicate)
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

                throw new InvalidOperationException($"Timeout reached before expected event count reached before prediction {predicate} came true, current diagnostics '{String.Join(";", Messages.SelectMany(x => x.Results))}'");
            }

            public DiagnosticTestEmitter()
            {
                _tcs = new TaskCompletionSource<object>();
            }

            public void Emit(string kind, object args)
            {
                Messages.Add((DiagnosticMessage)args);
            }
        }
    }
}
