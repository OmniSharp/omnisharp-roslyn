using System;
using System.Collections.Generic;
using Cake.Scripting.Abstractions;

namespace OmniSharp.Cake.Services
{
    public interface ICakeScriptService : IScriptGenerationService
    {
        bool IsConnected { get; }

        event EventHandler<ReferencesChangedEventArgs> ReferencesChanged;

        event EventHandler<UsingsChangedEventArgs> UsingsChanged;
    }

    public abstract class ScriptChangedEventArgs : EventArgs
    {
        public string ScriptPath { get; }

        protected ScriptChangedEventArgs(string scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                throw new ArgumentNullException(nameof(scriptPath));
            }

            ScriptPath = scriptPath;
        }
    }

    public class ReferencesChangedEventArgs : ScriptChangedEventArgs
    {
        public IReadOnlyCollection<string> References { get; }

        public ReferencesChangedEventArgs(string scriptPath, IReadOnlyCollection<string> references) : base(scriptPath)
        {
            References = references ?? new List<string>();
        }
    }

    public class UsingsChangedEventArgs : ScriptChangedEventArgs
    {
        public IReadOnlyCollection<string> Usings { get; }

        public UsingsChangedEventArgs(string scriptPath, IReadOnlyCollection<string> usings) : base(scriptPath)
        {
            Usings = usings ?? new List<string>();
        }
    }
}
