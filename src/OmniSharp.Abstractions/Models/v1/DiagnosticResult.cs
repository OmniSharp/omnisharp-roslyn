using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.Models
{
    public class DiagnosticResult
    {
        public string FileName { get; set; }
        public IEnumerable<DiagnosticLocation> QuickFixes { get; set; }
    }
}