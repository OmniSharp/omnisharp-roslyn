/*
# Original ported from:
#
# string_score.js: String Scoring Algorithm 0.1.10
#
# http://joshaven.com/string_score
# https://github.com/joshaven/string_score
#
# Copyright (C) 2009-2011 Joshaven Potter <yourtech@gmail.com>
# Special thanks to all of the contributors listed here https://github.com/joshaven/string_score
# MIT license: http://www.opensource.org/licenses/mit-license.php
#
# Date: Tue Mar 1 2011
*/
using System;

namespace FuzzySearch
{
    class Scorer
    {
        const char PathSeparator = '\\';

        public double BasenameScore(string str, string query, double score)
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
                        b = str.Substring(index, lastCharacter + 1);
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

            if (b == str)
            {
                score *= 2;
            }
            else if (!string.IsNullOrEmpty(b))
            {
                score += Score(b, query);
            }

            var segmentCount = slashCount + 1;
            var depth = Math.Max(1, 10 - segmentCount);
            score *= depth * 0.01;
            return score;
        }

        private bool queryIsLastPathSegment(string str, string query)
        {
            var projectedSep = str.Length - 1 - query.Length;
            if (projectedSep > -1 && projectedSep < str.Length)
            {
                if (str[projectedSep] == PathSeparator)
                {
                    return str.LastIndexOf(query) == str.Length - query.Length;
                }
            }
            return false;
        }

        public double Score(string str, string query)
        {
            if (str == query)
            {
                return 1;
            }

            if (queryIsLastPathSegment(str, query))
            {
                return 1;
            }

            var totalCharacterScore = 0.0;
            var queryLength = query.Length;
            var indexInQuery = 0;
            var indexInstr = 0;
            var strLength = str.Length;
            var ostr = str;

            while (indexInQuery < queryLength)
            {
                var character = query[indexInQuery++];
                var lowerCaseIndex = str.IndexOf(char.ToLowerInvariant(character));
                var upperCaseIndex = str.IndexOf(char.ToUpperInvariant(character));
                var minIndex = Math.Min(lowerCaseIndex, upperCaseIndex);

                if (minIndex == -1)
                {
                    minIndex = Math.Max(lowerCaseIndex, upperCaseIndex);
                }

                indexInstr = minIndex;
                if (indexInstr == -1)
                {
                    return 0;
                }

                var characterScore = 0.1;
                if (str[indexInstr] == character)
                {
                    characterScore += 0.1;
                }

                if (indexInstr == 0 || str[indexInstr - 1] == PathSeparator)
                {
                    characterScore += 0.8;
                }
                else if (str[indexInstr - 1] == '-' || str[indexInstr - 1] == '_' || str[indexInstr - 1] == ' ')
                {
                    characterScore += 0.7;
                }

                str = str.Substring(indexInstr + 1);
                totalCharacterScore += characterScore;
            }

            var queryScore = totalCharacterScore / queryLength;
            return ((queryScore * (queryLength / strLength)) + queryScore) / 2;
        }
    }
}
