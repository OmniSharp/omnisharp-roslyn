using System;
using System.Collections.Generic;
using System.Linq;

namespace FuzzySearch
{
    public class Nuzzaldrin
    {
        const char PathSeparator = '\\';
        private readonly Matcher _matcher;
        private readonly Scorer _scorer;
        private readonly Filter _filter;

        public Nuzzaldrin()
        {
            _scorer = new Scorer();
            _filter = new Filter(_scorer);
            _matcher = new Matcher();
        }

        public IEnumerable<T> Filter<T>(IEnumerable<T> candidates, Func<T, string> keySelector, string query, FilterOptions options = null)
        {
            if (options == null) options = new FilterOptions();
            bool queryHasSlashes = false;
            if (!string.IsNullOrEmpty(query))
            {
                queryHasSlashes = query.IndexOf(PathSeparator) == -1;
                query = query.Replace(" ", string.Empty);
            }
            return _filter.Method(candidates, keySelector, query, queryHasSlashes, options);
        }

        public double Score(string str, string query)
        {
            if (string.IsNullOrEmpty(str))
            {
                return 0;
            }
            
            if (string.IsNullOrEmpty(query))
            {
                return 1;
            }

            if (str == query)
            {
                return 2;
            }

            var queryHasSlashes = query.IndexOf(PathSeparator) == -1;
            query = query.Replace(" ", string.Empty);

            var score = _scorer.Score(str, query);
            if (!queryHasSlashes)
            {
                score = _scorer.BasenameScore(str, query, score);
            }
            return score;
        }

        public IEnumerable<int> Match(string str, string query)
        {
            if (string.IsNullOrEmpty(str))
            {
                return Enumerable.Empty<int>();
            }
            if (string.IsNullOrEmpty(query))
            {
                return Enumerable.Empty<int>();
            }
            if (str == query)
            {
                return str.Cast<int>();
            }
            var queryHasSlashes = query.IndexOf(PathSeparator) == -1;
            query = query.Replace(" ", "");
            var matches = _matcher.Match(str, query, null).ToList();
            if (!queryHasSlashes)
            {
                var baseMatches = _matcher.BasenameMatch(str, query);
                matches = matches.Concat(baseMatches).OrderBy(x => x).ToList();
                int? seen = null;
                var index = 0;
                while (index < matches.Count)
                {
                    if (index != 0 && seen.HasValue && seen == matches[index])
                    {
                        matches.RemoveAt(index);
                    }
                    else
                    {
                        seen = matches[index];
                        index++;
                    }
                }
            }
            return matches;
        }
    }
}
