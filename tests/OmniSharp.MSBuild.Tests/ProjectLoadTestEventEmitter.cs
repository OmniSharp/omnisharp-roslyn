using OmniSharp.Eventing;
using OmniSharp.Models.Events;
using System.Collections.Generic;

namespace OmniSharp.MSBuild.Tests
{
    public partial class ProjectLoadListenerTests
    {
        public class ProjectLoadTestEventEmitter : IEventEmitter
        {
            private readonly IList<ProjectConfigurationMessage> _messages;

            public ProjectLoadTestEventEmitter(IList<ProjectConfigurationMessage> messages)
            {
                _messages = messages;
            }

            public void Emit(string kind, object args)
            {
                _messages.Add((ProjectConfigurationMessage)args);
            }
        }
    }
}
