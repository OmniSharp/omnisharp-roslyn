/*
# A match list is an array of indexes to characters that match.
# This file should closely follow `scorer` except that it returns an array
# of indexes instead of a score.
*/
using System;
using System.Collections.Generic;
using System.Linq;

namespace FuzzySearch
{
    class Matcher
    {
        const char PathSeparator = '\\';

        public IEnumerable<int> BasenameMatch(string str, string query)
        {
            var index = str.Length - 1;
            while (str[index] == PathSeparator)
            {
                index--;
            }
            var slashCount = 0;
            var lastCharacter = index;
            string b = null;
            while (index >= 0)
            {
                if (str[index] == PathSeparator)
                {
                    slashCount++;
                    if (b == null)
                    {
                        b = str.Substring(index + 1, lastCharacter + 1 - index + 1);
                    }
                }
                else if (index == 0)
                {
                    if (lastCharacter < str.Length - 1)
                    {
                        if (b == null)
                        {
                            b = str.Substring(0, lastCharacter + 1);
                        }
                    }
                    else
                    {
                        if (b == null)
                        {
                            b = str;
                        }
                    }
                }
                index--;
            }
            return Match(b, query, str.Length - b.Length);
        }

        public IEnumerable<int> Match(string str, string query, int? strOffset)
        {
            if (!strOffset.HasValue)
            {
                strOffset = 0;
            }
            if (str == query)
            {
                return str.Cast<int>();
            }
            var queryLength = query.Length;
            var strLength = str.Length;
            var indexInQuery = 0;
            var indexInString = 0;
            var matches = new List<int>();
            while (indexInQuery < queryLength)
            {
                var character = query[indexInQuery++];
                var lowerCaseIndex = str.IndexOf(char.ToLower(character));
                var upperCaseIndex = str.IndexOf(char.ToUpper(character));
                var minIndex = Math.Min(lowerCaseIndex, upperCaseIndex);
                if (minIndex == -1)
                {
                    minIndex = Math.Max(lowerCaseIndex, upperCaseIndex);
                }
                indexInString = minIndex;
                if (indexInString == -1)
                {
                    return Enumerable.Empty<int>();
                }
                matches.Add(strOffset.Value + indexInString);
                strOffset += indexInString + 1;
                str = str.Substring(indexInString + 1, strLength - indexInString + 1);
            }
            return matches;
        }
    }
}
