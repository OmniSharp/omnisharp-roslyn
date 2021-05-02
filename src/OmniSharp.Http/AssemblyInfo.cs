using OmniSharp;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OmniSharp" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("TestUtility" + OmniSharpPublicKey.Key)]
[assembly: InternalsVisibleTo("OmniSharp.Http.Tests" + OmniSharpPublicKey.Key)]
