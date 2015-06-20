// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DependenciesMessage
    {
        public FrameworkData Framework { get; set; }
        public string RootDependency { get; set; }
        public IDictionary<string, DependencyDescription> Dependencies { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as DependenciesMessage;

            return other != null &&
                   string.Equals(RootDependency, other.RootDependency) &&
                   object.Equals(Framework, other.Framework) &&
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