// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ReferencesMessage
    {
        public FrameworkData Framework { get; set; }
        public IList<ProjectReference> ProjectReferences { get; set; }
        public IList<string> FileReferences { get; set; }
        public IDictionary<string, byte[]> RawReferences { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ReferencesMessage;

            return other != null &&
                   object.Equals(Framework, other.Framework) &&
                   Enumerable.SequenceEqual(ProjectReferences, other.ProjectReferences) &&
                   Enumerable.SequenceEqual(FileReferences, other.FileReferences) &&
                   Enumerable.SequenceEqual(RawReferences, other.RawReferences);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}