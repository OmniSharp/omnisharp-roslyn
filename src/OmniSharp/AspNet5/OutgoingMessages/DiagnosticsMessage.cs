// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DiagnosticsMessage
    {
        public FrameworkData Framework { get; set; }

        public IList<string> Warnings { get; set; }
        public IList<string> Errors { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as DiagnosticsMessage;

            return other != null &&
                 Enumerable.SequenceEqual(Warnings, other.Warnings) &&
                 Enumerable.SequenceEqual(Errors, other.Errors);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}