// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class ExportFile
    {
        public static readonly string ExportFileName = "project.fragment.lock.json";

        public int Version { get; }
        public string ExportFilePath { get; }

        public IList<LockFileTargetLibrary> Exports { get; }

        public ExportFile(string exportFilePath, int version, IList<LockFileTargetLibrary> exports)
        {
            ExportFilePath = exportFilePath;
            Version = version;
            Exports = exports.Any() ? exports : new List<LockFileTargetLibrary>(0);
        }
    }
}
