// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.DesignTimeHost.Models.IncomingMessages
{
    public class InitializeMessage
    {
        public string Configuration { get; set; }

        public string ProjectFolder { get; set; }
    }
}