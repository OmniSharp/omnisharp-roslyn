using System.Collections.Generic;

namespace OmniSharp.Cake
{
    internal class CakeContextModelCollection
    {
        public CakeContextModelCollection(IEnumerable<CakeContextModel> projects)
        {
            Projects = projects;
        }

        public IEnumerable<CakeContextModel> Projects { get; }
    }
}