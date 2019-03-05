using OmniSharp.Models;
using System.Text;

namespace OmniSharp.MSBuild
{
    public class VsReferenceHashingAlgorithm : IHasher
    {
        public HashedString HashInput(string clearText)
        {
            return new HashedString(clearText);
        }
    }
}
