#nullable enable

using OmniSharp.Models.V2;
using System.Collections.Generic;

namespace OmniSharp.Models.v1.InlineValues
{
    public class InlineValuesResponse
    {
        public List<InlineValue>? Values { get; init; }
    }

    public record InlineValue
    {
        public InlineValueKind Kind { get; init; }
        /// <summary>
        /// Not nullable when <see cref="InlineValueKind.Text"/>.
        /// </summary>
        public string? Text { get; init; }
        public Range Range { get; init; } = null!;
        /// <summary>
        /// Only has meaning when <see cref="Kind"/> is <see cref="InlineValueKind.VariableLookup"/>
        /// </summary>
        public bool CaseSensitiveLookup { get; init; }
    }

    public enum InlineValueKind
    {
        /// <summary>
        /// <see cref="InlineValue.Text"/> represents raw text to display. It should not be null.
        /// </summary>
        Text,
        /// <summary>
        /// <see cref="InlineValue.Text"/> represents the variable to look up and display. If null,
        /// the underlying text of <see cref="InlineValue.Range"/> is used. <see cref="InlineValue.CaseSensitiveLookup"/>
        /// determines whether the variable lookup is case sensitive.
        /// </summary>
        VariableLookup,
        /// <summary>
        /// <see cref="InlineValue.Text"/> represents an expression to for the debugger to evaluate.
        /// If null, the underlying text of <see cref="InlineValue.Range"/> is used.
        /// </summary>
        EvaluatableExpression
    }
}
