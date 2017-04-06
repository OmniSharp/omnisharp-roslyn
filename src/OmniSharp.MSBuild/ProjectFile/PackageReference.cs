using System;
using NuGet.Packaging.Core;

namespace OmniSharp.MSBuild.ProjectFile
{
    public class PackageReference : IEquatable<PackageReference>
    {
        public PackageDependency Identity { get; }
        public bool IsImplicitlyDefined { get; }

        public PackageReference(PackageDependency dependency, bool isImplicitlyDefined)
        {
            this.Identity = dependency;
            this.IsImplicitlyDefined = isImplicitlyDefined;
        }

        public override string ToString()
        {
            var implicitSuffix = IsImplicitlyDefined ? " (implicit)" : string.Empty;

            return Identity.Id + ", " + Identity.VersionRange + implicitSuffix;
        }

        public bool Equals(PackageReference other)
        {
            if (!Identity.Equals(other.Identity))
            {
                return false;
            }

            return IsImplicitlyDefined == other.IsImplicitlyDefined;
        }

        public override int GetHashCode()
        {
            return this.Identity.GetHashCode() + (IsImplicitlyDefined ? 1 : 0);
        }
    }
}
