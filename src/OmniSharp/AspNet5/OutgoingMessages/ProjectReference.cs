using System;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ProjectReference
    {
        public FrameworkData Framework { get; set; }
        public string Path { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ProjectReference;
            return other != null &&
                   string.Equals(Framework, other.Framework) &&
                   object.Equals(Path, other.Path);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}