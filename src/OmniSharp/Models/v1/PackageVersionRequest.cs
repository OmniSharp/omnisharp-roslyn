using System;

namespace OmniSharp.Models
{
    public class PackageVersionRequest
    {
        /// <summary>
        /// The path to the project file
        /// </summary>
        public string ProjectPath { get; set; }

        /// <summary>
        /// The id of the package to look up the versions
        /// </summary>
        public string Id { get; set; }
    }
}
