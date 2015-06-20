// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class FrameworkData
    {
        public string FrameworkName { get; set; }
        public string FriendlyName { get; set; }
        public string ShortName { get; set; }
        public string RedistListPath { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as FrameworkData;

            return other != null &&
                   string.Equals(FrameworkName, other.FrameworkName);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}