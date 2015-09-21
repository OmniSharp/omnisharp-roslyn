using OmniSharp.Mef;

﻿namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/rename", typeof(RenameRequest), typeof(RenameResponse))]
    public class RenameRequest : Request
    {
        /// <summary>
        ///  When true, return just the text changes.
        /// </summary>
        public bool WantsTextChanges { get; set; }

        /// <summary>
        ///  When true, apply changes immediately on the server.
        /// </summary>
        public bool ApplyTextChanges { get; set; } = true;

        public string RenameTo { get; set; }
    }
}
