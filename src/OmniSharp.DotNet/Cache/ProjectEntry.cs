using System.Collections.Generic;
using NuGet.Frameworks;

namespace OmniSharp.DotNet.Cache
{
    public class ProjectEntry
    {
        private readonly Dictionary<NuGetFramework, ProjectState> _states
                   = new Dictionary<NuGetFramework, ProjectState>();

        public ProjectEntry(string projectDirectory)
        {
            ProjectDirectory = projectDirectory;
        }

        public string ProjectDirectory { get; }

        public IEnumerable<NuGetFramework> Frameworks => _states.Keys;

        public IEnumerable<ProjectState> ProjectStates => _states.Values;

        public ProjectState Get(NuGetFramework framework)
        {
            ProjectState result;
            if (_states.TryGetValue(framework, out result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        public void Set(ProjectState state)
        {
            _states[state.ProjectContext.TargetFramework] = state;
        }

        public bool Remove(NuGetFramework framework)
        {
            return _states.Remove(framework);
        }

        public override string ToString()
        {
            return $"ProjectEntry {ProjectDirectory}, {_states.Count} states";
        }
    }
}