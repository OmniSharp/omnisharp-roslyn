using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Models
{
    public enum HighlightClassification
    {
        Name = 1,
        Comment = 2,
        String = 3,
        Operator = 4,
        Punctuation = 5,
        Keyword = 6,
        Number = 7,
        Identifier = 8,
        PreprocessorKeyword = 9,
        ExcludedCode = 10
    }
}
