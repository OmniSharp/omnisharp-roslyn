// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class SourcesMessage
    {
        public FrameworkData Framework { get; set; }

        public IList<string> Files { get; set; }
        public IDictionary<string, string> GeneratedFiles { get; set; }


        public override bool Equals(object obj)
        {
            var other = obj as SourcesMessage;

            return other != null &&
                   Enumerable.SequenceEqual(Files, other.Files);

        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}
