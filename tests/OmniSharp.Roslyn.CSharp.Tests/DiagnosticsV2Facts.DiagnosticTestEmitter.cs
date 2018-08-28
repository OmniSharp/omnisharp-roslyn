using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Eventing;
using OmniSharp.Models.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public partial class DiagnosticsV2Facts
    {
        private class DiagnosticTestEmitter : IEventEmitter
        {
            public readonly List<DiagnosticMessage> Messages = new List<DiagnosticMessage>();
            private readonly TaskCompletionSource<object> _tcs;

            public async Task WaitForEmitted(int expectedCount = 1)
            {
                // May seem hacky but nothing is more painfull to debug than infinite hanging test ...
                for(int i = 0; i < 100; i++)
                {
                    if(Messages.Count == expectedCount)
                    {
                        return;
                    }

                    await Task.Delay(50);
                }

                throw new InvalidOperationException($"Timeout reached before expected event count reached, expected '{expectedCount}' got '{Messages.Count}' ");
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
