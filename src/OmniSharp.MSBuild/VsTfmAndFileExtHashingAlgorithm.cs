using OmniSharp.Models;

namespace OmniSharp.MSBuild
{
    public class VsTfmAndFileExtHashingAlgorithm : IHasher
    {
        public HashedString HashInput(string clearText)
        {
            return new HashedString(clearText);
        }
    }
}
