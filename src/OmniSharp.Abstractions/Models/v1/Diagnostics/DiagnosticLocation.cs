using System.Collections.Generic;

namespace OmniSharp.Models.Diagnostics
{
    public class DiagnosticLocation : QuickFix
    {
        public string LogLevel { get; set; }
        public string Id { get; set; }
        public string[] Tags { get; set; }

        public override bool Equals(object obj)
        {
            var location = obj as DiagnosticLocation;
            return location != null &&
                   base.Equals(obj) &&
                   LogLevel == location.LogLevel &&
                   Id == location.Id;
        }

        public override int GetHashCode()
        {
            var hashCode = -1670479257;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(LogLevel);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Id);
            return hashCode;
        }
    }
}
