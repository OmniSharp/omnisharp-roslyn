using System;
using System.Collections.Generic;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/packageversion", typeof(PackageVersionRequest), typeof(PackageVersionResponse))]
    public class PackageVersionRequest : IRequest
    {
        /// <summary>
        /// The path to the project file
        /// </summary>
        public string ProjectPath { get; set; }

        /// <summary>
        /// The sources to search for the given package
        /// </summary>
        public IEnumerable<string> Sources { get; set; }

        /// <summary>
        /// The id of the package to look up the versions
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Include pre-release version numbers
        /// <summary>
        public bool IncludePrerelease { get; set; } = true;
    }
}
