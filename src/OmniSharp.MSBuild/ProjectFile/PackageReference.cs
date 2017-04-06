using System;
using NuGet.Packaging.Core;

namespace OmniSharp.MSBuild.ProjectFile
{
    public class PackageReference : IEquatable<PackageReference>
    {
        public PackageDependency Dependency { get; }
        public bool IsImplicitlyDefined { get; }

        public PackageReference(PackageDependency dependency, bool isImplicitlyDefined)
        {
            this.Dependency = dependency;
            this.IsImplicitlyDefined = isImplicitlyDefined;
        }

        public override string ToString()
        {
            var implicitSuffix = IsImplicitlyDefined ? " (implicit)" : string.Empty;

            return Dependency.Id + ", " + Dependency.VersionRange + implicitSuffix;
        }

        public bool Equals(PackageReference other)
        {
            if (!Dependency.Equals(other.Dependency))
            {
                return false;
            }

            return IsImplicitlyDefined == other.IsImplicitlyDefined;
        }

        public override int GetHashCode()
        {
            return this.Dependency.GetHashCode() + (IsImplicitlyDefined ? 1 : 0);
        }
    }
}
