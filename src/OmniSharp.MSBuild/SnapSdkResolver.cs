// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.IO;

using SdkResolverBase = Microsoft.Build.Framework.SdkResolver;
using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;
using SdkResultFactoryBase = Microsoft.Build.Framework.SdkResultFactory;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    ///     Resolve SDKs that are part of a Snap installation
    /// <remarks>
    ///     This will search for the Snap SDK installation of the .NET Core SDK
    ///     by executing `dotnet --list-sdks`.
    /// </remarks>
    /// </summary>
    internal class SnapSdkResolver : SdkResolverBase
    {
        public override string Name => "SnapSdkResolver";

        public override int Priority => 7000;

        public override SdkResultBase Resolve(SdkReference sdk, SdkResolverContextBase context, SdkResultFactoryBase factory)
        {



            // Start the child process.
            System.Diagnostics.Process  p = new System.Diagnostics.Process();

            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "dotnet";
            p.StartInfo.Arguments = "--list-sdks";
            p.Start();
            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // p.WaitForExit();
            // Read the output stream first and then wait.
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            var version = output.Substring(0, output.IndexOf("[")-2);
            var sdkPath = output.Substring(output.IndexOf("["), output.Length-1);
            var fullSdkPath = Path.Combine(sdkPath, version, sdk.Name, "Sdk");

            // Note: On failure MSBuild will log a generic message, no need to indicate a failure reason here.
            return Directory.Exists(fullSdkPath)
                ? factory.IndicateSuccess(fullSdkPath, string.Empty)
                : factory.IndicateFailure(null);
        }
    }
}