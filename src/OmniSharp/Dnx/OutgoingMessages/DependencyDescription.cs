// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DependencyDescription
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public string Path { get; set; }

        public string Type { get; set; }

        public IEnumerable<DependencyItem> Dependencies { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as DependencyDescription;

            return other != null &&
                   string.Equals(Name, other.Name) &&
                   object.Equals(Version, other.Version) &&
                   string.Equals(Path, other.Path) &&
                   string.Equals(Type, other.Type) &&
                   Enumerable.SequenceEqual(Dependencies, other.Dependencies);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}