namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ProjectReference
    {
        public FrameworkData Framework { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string WrappedProjectPath { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ProjectReference;
            return other != null &&
                   object.Equals(Framework, other.Framework) &&
                   string.Equals(Name, other.Name) &&
                   string.Equals(Path, other.Path) &&
                   string.Equals(WrappedProjectPath, other.WrappedProjectPath);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}