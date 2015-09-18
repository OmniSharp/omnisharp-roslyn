using Microsoft.Dnx.Compilation.CSharp;
using Microsoft.Framework.Runtime.Roslyn;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class CompilationOptionsMessage
    {
        public FrameworkData Framework { get; set; }

#pragma warning disable CS0436 // Type conflicts with imported type
        public CompilationSettings CompilationOptions { get; set; }
#pragma warning restore CS0436 // Type conflicts with imported type

        public override bool Equals(object obj)
        {
            var other = obj as CompilationOptionsMessage;

            return other != null &&
                 object.Equals(Framework, other.Framework) &&
                 object.Equals(CompilationOptions, other.CompilationOptions);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}
