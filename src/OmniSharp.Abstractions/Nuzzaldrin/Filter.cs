//scorer = require('./scorer');

using System;
using System.Collections.Generic;
using System.Linq;

namespace FuzzySearch
{
    public class FilterOptions
    {
        public int? MaxResults { get; set; }
    }

    class Candidate<T>
    {
        public T Value { get; set; }
        public double Score { get; set; }
    }

    class Filter
    {
        private readonly Scorer _scorer;

        public Filter(Scorer scorer)
        {
            _scorer = scorer;
        }

        public IEnumerable<T> Method<T>(IEnumerable<T> candidates, Func<T, string> keySelector, string query, bool queryHasSlashes, FilterOptions options)
        {
            var maxResults = options.MaxResults;

            if (!string.IsNullOrEmpty(query))
            {
                var scoredCandidates = new HashSet<Candidate<T>>();
                foreach (var candidate in candidates)
                {
                    var key = keySelector(candidate);
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    var score = _scorer.Score(key, query);
                    if (!queryHasSlashes)
                    {
                        score = _scorer.BasenameScore(key, query, score);
                    }
                    if (score > 0)
                    {
                        scoredCandidates.Add(new Candidate<T>
                        {
                            Value = candidate,
                            Score = score
                        });
                    }
                }

                candidates = scoredCandidates
                    .OrderBy(x => x.Score)
                    .Select(x => x.Value);
            }

            if (maxResults.HasValue)
            {
                candidates = candidates.Take(maxResults.Value);
            }

            return candidates;
        }
    }
}
