using OmniSharp;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OmniSharp" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Host" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.MSBuild" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Roslyn" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Roslyn.CSharp" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.DotNetTest.Tests" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Tests" + OmniSharpPublicKey.Key)]
