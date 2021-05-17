using OmniSharp;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestUtility" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Stdio.Tests" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Lsp.Tests" + OmniSharpPublicKey.Key)]
