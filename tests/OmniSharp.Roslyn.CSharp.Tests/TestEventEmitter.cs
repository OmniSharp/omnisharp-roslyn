using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OmniSharp.Eventing;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TestEventEmitter<T> : IEventEmitter
    {
        private readonly object _lock = new();
        private readonly List<T> _messages = new();
        private readonly List<(Predicate<T> Predicate, TaskCompletionSource<object> TaskCompletionSource)> _predicates = new();

        public async Task ExpectForEmitted(Expression<Predicate<T>> predicate)
        {
            var asCompiledPredicate = predicate.Compile();
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lock)
            {
                if (_messages.Any(m => asCompiledPredicate(m)))
                    return;

                _predicates.Add((asCompiledPredicate, tcs));
            }

            try
            {
                using var cts = new CancellationTokenSource(25000);

                cts.Token.Register(() => tcs.SetCanceled());

                await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                var messages = string.Join(";", _messages.Select(x => JsonConvert.SerializeObject(x)));

                throw new InvalidOperationException($"Timeout reached before expected event count reached before prediction {predicate} came true, current diagnostics '{messages}'");
            }
            finally
            {
                lock (_lock)
                    _predicates.Remove((asCompiledPredicate, tcs));
            }
        }

        public void Clear()
        {
            lock (_lock)
                _messages.Clear();
        }

        public void Emit(string kind, object args)
        {
            if (args is T asT)
            {
                lock (_lock)
                {
                    _messages.Add(asT);

                    foreach (var (predicate, tcs) in _predicates)
                    {
                        if (predicate(asT))
                            tcs.SetResult(null);
                    }
                }
            }
        }
    }
}
