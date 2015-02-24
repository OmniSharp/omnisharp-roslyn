// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DependencyItem
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as DependencyItem;
            return other != null &&
                   string.Equals(Name, other.Name) &&
                   object.Equals(Version, other.Version);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}