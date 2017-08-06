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
        private static Type s_VisualStudioLocationHelperType;

        static MSBuildHelpers()
        {
            s_MicrosoftBuildAssembly = Assembly.Load(new AssemblyName("Microsoft.Build"));

            s_BuildEnvironmentHelperType = s_MicrosoftBuildAssembly.GetType("Microsoft.Build.Shared.BuildEnvironmentHelper");
            s_BuildEnvironmentType = s_MicrosoftBuildAssembly.GetType("Microsoft.Build.Shared.BuildEnvironment");
            s_VisualStudioLocationHelperType = s_MicrosoftBuildAssembly.GetType("Microsoft.Build.Shared.VisualStudioLocationHelper");
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
            var propValue = GetPropertyValue(name, instance, type, bindingFlags);
            builder.AppendLine($"{name}: {propValue}");
        }

        private static object GetPropertyValue(string name, object instance, Type type, BindingFlags bindingFlags)
        {
            var propInfo = type.GetProperty(name, bindingFlags);
            return propInfo.GetMethod.Invoke(instance, null);
        }

        public static bool CanInitializeVisualStudioBuildEnvironment()
        {
            if (!PlatformHelper.IsWindows)
            {
                return false;
            }

            // Call Microsoft.Build.Shared.BuildEnvironmentHelper.Initialze(...), which attempts to compute a build environment..
            var initializeMethod = s_BuildEnvironmentHelperType.GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static);
            var buildEnvironment = initializeMethod.Invoke(null, null);

            if (buildEnvironment == null)
            {
                return false;
            }

            var mode = GetPropertyValue("Mode", buildEnvironment, s_BuildEnvironmentType, BindingFlags.NonPublic | BindingFlags.Instance);

            return mode?.ToString() == "VisualStudio";
        }
    }
}
