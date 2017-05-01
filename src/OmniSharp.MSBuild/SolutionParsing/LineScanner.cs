// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// This is a modified copy originally taken from https://github.com/dotnet/roslyn/blob/0d8c31c4531080a822749cd87d6ce77a20ca28ef/src/Workspaces/Core/Desktop/Workspace/MSBuild/SolutionFile/LineScanner.cs

using System;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal class LineScanner
    {
        private readonly string _line;
        private int _currentPosition;

        public LineScanner(string line)
        {
            _line = line;
        }

        public string ReadUpToAndEat(string delimiter)
        {
            int index = _line.IndexOf(delimiter, _currentPosition, StringComparison.Ordinal);

            if (index == -1)
            {
                return ReadRest();
            }
            else
            {
                var upToDelimiter = _line.Substring(_currentPosition, index - _currentPosition);
                _currentPosition = index + delimiter.Length;
                return upToDelimiter;
            }
        }

        public string ReadRest()
        {
            var rest = _line.Substring(_currentPosition);
            _currentPosition = _line.Length;
            return rest;
        }
    }
}
