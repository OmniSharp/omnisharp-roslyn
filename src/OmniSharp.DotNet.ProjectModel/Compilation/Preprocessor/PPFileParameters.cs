// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectModel.Compilation.Preprocessor
{
    public class PPFileParameters
    {
        public static IDictionary<string, string> CreateForProject(Project project)
        {
            return new Dictionary<string, string>()
            {
                {"rootnamespace", project.Name },
                {"assemblyname", project.Name }
            };
        }
    }
}
