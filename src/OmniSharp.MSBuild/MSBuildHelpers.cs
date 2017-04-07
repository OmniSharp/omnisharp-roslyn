using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild
{
    public static class MSBuildHelpers
    {
        private static Assembly s_MicrosoftBuildAssembly;

        private static Type s_BuildEnvironmentHelperType;
        private static Type s_BuildEnvironmentType;

        static MSBuildHelpers()
        {
            s_MicrosoftBuildAssembly = Assembly.Load(new AssemblyName("Microsoft.Build"));

            s_BuildEnvironmentHelperType = s_MicrosoftBuildAssembly.GetType("Microsoft.Build.Shared.BuildEnvironmentHelper");
            s_BuildEnvironmentType = s_MicrosoftBuildAssembly.GetType("Microsoft.Build.Shared.BuildEnvironment");
        }

        public static string GetBuildEnvironmentInfo()
        {
            var instanceProp = s_BuildEnvironmentHelperType.GetProperty("Instance");
            var buildEnvironment = instanceProp.GetMethod.Invoke(null, null);

            return DumpBuildEnvironment(buildEnvironment);
        }

        private static string DumpBuildEnvironment(object buildEnvironment)
        {
            var builder = new StringBuilder();

            if (buildEnvironment != null)
            {
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

                AppendPropertyValue(builder, "Mode", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "RunningTests", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "RunningInVisualStudio", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "MSBuildToolsDirectory32", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "MSBuildToolsDirectory64", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "MSBuildSDKsPath", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "CurrentMSBuildConfigurationFile", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "CurrentMSBuildExePath", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "CurrentMSBuildToolsDirectory", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "VisualStudioInstallRootDirectory", buildEnvironment, s_BuildEnvironmentType, flags);
                AppendPropertyValue(builder, "MSBuildExtensionsPath", buildEnvironment, s_BuildEnvironmentType, flags);
            }

            return builder.ToString();
        }

        private static void AppendPropertyValue(StringBuilder builder, string name, object instance, Type type, BindingFlags bindingFlags)
        {
            var propInfo = type.GetProperty(name, bindingFlags);
            var propValue = propInfo.GetMethod.Invoke(instance, null);
            builder.AppendLine($"{name}: {propValue}");
        }

        public static bool TryGetVisualStudioBuildEnvironment()
        {
            if (!PlatformHelper.IsWindows)
            {
                return false;
            }

            // Call Microsoft.Build.Shared.BuildEnvironmentHelper.TryFromSetupApi(...), which attempts
            // to compute a build environment by looking for VS 2017.
            var tryFromSetupApiMethod = s_BuildEnvironmentHelperType.GetMethod("TryFromSetupApi", BindingFlags.NonPublic | BindingFlags.Static);
            var buildEnvironment = tryFromSetupApiMethod.Invoke(null, null);

            return buildEnvironment != null;
        }
    }
}
