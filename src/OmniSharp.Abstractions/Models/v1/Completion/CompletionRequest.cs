#nullable enable

using System.ComponentModel;
using OmniSharp.Mef;

namespace OmniSharp.Models.v1.Completion
{
    [OmniSharpEndpoint(OmniSharpEndpoints.Completion, typeof(CompletionRequest), typeof(CompletionResponse))]
    public class CompletionRequest : Request
    {
        /// <summary>
        /// How the completion was triggered
        /// </summary>
        public CompletionTriggerKind CompletionTrigger { get; set; }

        /// <summary>
        /// The character that triggered completion if <see cref="CompletionTrigger"/>
        /// is <see cref="CompletionTriggerKind.TriggerCharacter"/>. <see langword="null"/>
        /// otherwise.
        /// </summary>
        public char? TriggerCharacter { get; set; }
    }

    public enum CompletionTriggerKind
    {
        /// <summary>
        /// Completion was triggered by typing an identifier (24x7 code
	    /// complete), manual invocation (e.g Ctrl+Space) or via API
        /// </summary>
        Invoked = 1,
        /// <summary>
        /// Completion was triggered by a trigger character specified by
	    /// the `triggerCharacters` properties of the `CompletionRegistrationOptions`.
        /// </summary>
        TriggerCharacter = 2,

        // We don't need to support incomplete completion lists that need to be recomputed
        // later, but this is reserving the number to match LSP if we need it later.
        [EditorBrowsable(EditorBrowsableState.Never)]
        TriggerForIncompleteCompletions = 3
    }
}
