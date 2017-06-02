using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Completion;

namespace OmniSharp.Roslyn.CSharp.Services.IntelliSense.V2
{
    using Models = OmniSharp.Models.V2.Completion;

    internal static class Extensions
    {
        public static CompletionTrigger ToRoslynCompletionTrigger(this Models.CompletionTrigger trigger)
        {
            if (trigger.Kind == Models.CompletionTriggerKind.Deletion ||
                trigger.Kind == Models.CompletionTriggerKind.Insertion &&
                trigger.Character == null)
            {
                throw new ArgumentException($"'{trigger.Kind}' completion triggers must provide a Character value.");
            }

            if (trigger.Character != null &&
                trigger.Character.Length != 1)
            {
                throw new ArgumentException($"Invalid trigger character: {trigger.Character}. Should have a length of 1.");
            }

            switch (trigger.Kind)
            {
                case Models.CompletionTriggerKind.Invoke:
                    return CompletionTrigger.Default;
                case Models.CompletionTriggerKind.Insertion:
                    return CompletionTrigger.CreateInsertionTrigger(trigger.Character[0]);
                case Models.CompletionTriggerKind.Deletion:
                    return CompletionTrigger.CreateDeletionTrigger(trigger.Character[0]);

                default:
                    throw new ArgumentException($"Invalid completion trigger kind encountered: {trigger.Kind}");
            }
        }

        private static readonly ImmutableArray<string> s_kindTags = ImmutableArray.Create(
            CompletionTags.Class,
            CompletionTags.Constant,
            CompletionTags.Delegate,
            CompletionTags.Enum,
            CompletionTags.EnumMember,
            CompletionTags.Event,
            CompletionTags.ExtensionMethod,
            CompletionTags.Field,
            CompletionTags.Interface,
            CompletionTags.Intrinsic,
            CompletionTags.Keyword,
            CompletionTags.Label,
            CompletionTags.Local,
            CompletionTags.Method,
            CompletionTags.Module,
            CompletionTags.Namespace,
            CompletionTags.Operator,
            CompletionTags.Parameter,
            CompletionTags.Property,
            CompletionTags.RangeVariable,
            CompletionTags.Reference,
            CompletionTags.Structure,
            CompletionTags.TypeParameter);

        public static string GetKind(this CompletionItem completionItem)
        {
            foreach (var tag in s_kindTags)
            {
                if (completionItem.Tags.Contains(tag))
                {
                    return tag;
                }
            }

            return null;
        }
    }
}
