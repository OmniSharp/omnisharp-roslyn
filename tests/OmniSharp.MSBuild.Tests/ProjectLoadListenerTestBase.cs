using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using TestUtility;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public abstract class ProjectLoadListenerTestBase : AbstractMSBuildTestFixture
    {
        private readonly VsTfmAndFileExtHashingAlgorithm _tfmAndFileHashingAlgorithm;
        private readonly VsReferenceHashingAlgorithm _referenceHashingAlgorithm;

        protected ProjectLoadListenerTestBase(ITestOutputHelper output) : base(output)
        {
            _tfmAndFileHashingAlgorithm = new VsTfmAndFileExtHashingAlgorithm();
            _referenceHashingAlgorithm = new VsReferenceHashingAlgorithm();
        }

        protected string GetHashedFileExtension(string fileExtension)
        {
            return _tfmAndFileHashingAlgorithm.HashInput(fileExtension).Value;
        }
        protected string GetHashedReference(string reference)
        {
            return _referenceHashingAlgorithm.HashInput(reference).Value;
        }
    }
}
