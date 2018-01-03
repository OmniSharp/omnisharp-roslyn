using System;
using System.Collections.Generic;
using Cake.Scripting.Abstractions;
using Microsoft.Extensions.Configuration;

namespace OmniSharp.Cake.Services
{
    public interface ICakeScriptService : IScriptGenerationService
    {
        bool Initialize(CakeOptions options);

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
        public ISet<string> References { get; }

        public ReferencesChangedEventArgs(string scriptPath, ISet<string> references) : base(scriptPath)
        {
            References = references ?? new HashSet<string>();
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
