using System.Runtime.CompilerServices;
using OmniSharp;

[assembly: InternalsVisibleTo("OmniSharp" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Host" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.MSBuild" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Roslyn" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Roslyn.CSharp" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.DotNetTest.Tests" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Tests" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.LanguageServerProtocol" + OmniSharpPublicKey.Key)]


namespace OmniSharp
{
    public class OmniSharpPublicKey
    {
        public const string Key = ", PublicKey=" + "0024000004800000940000000602000000240000525341310004000001000100917302efc152e6" +
                                                   "464679d4625bd9989e12d4662a9eaadf284d04992881c0e7b16e756e63ef200a02c4054d4d31e2" +
                                                   "1b9aa0b0b873bcefca8cd42ec583a3db509665c9b22318ceceec581663fc07e2422bb2135539ba" +
                                                   "8a517c209ac175fff07c5af10cef636e04cae91d28f51fcde5d14c1a9bfed06e096cf977fd0d60" +
                                                   "002a3ea6";
    }
}
