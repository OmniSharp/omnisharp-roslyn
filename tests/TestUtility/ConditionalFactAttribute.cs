using System;
using System.Runtime.InteropServices;
using OmniSharp.Utilities;
using Xunit;

namespace TestUtility
{
    public class ConditionalFactAttribute : FactAttribute
    {
        public ConditionalFactAttribute(params Type[] skipConditionTypes)
        {
            foreach (var skipConditionType in skipConditionTypes)
            {
                var skipCondition = (SkipCondition)Activator.CreateInstance(skipConditionType);
                if (skipCondition.ShouldSkip)
                {
                    Skip = skipCondition.SkipReason;
                    break;
                }
            }
        }
    }

    public class ConditionalTheoryAttribute : TheoryAttribute
    {
        public ConditionalTheoryAttribute(params Type[] skipConditionTypes)
        {
            foreach (var skipConditionType in skipConditionTypes)
            {
                var skipCondition = (SkipCondition)Activator.CreateInstance(skipConditionType);
                if (skipCondition.ShouldSkip)
                {
                    Skip = skipCondition.SkipReason;
                    break;
                }
            }
        }
    }

    public abstract class SkipCondition
    {
        public abstract bool ShouldSkip { get; }
        public abstract string SkipReason { get; }
    }

    public class NotOnAppVeyor : SkipCondition
    {
        public override bool ShouldSkip => string.Equals(Environment.GetEnvironmentVariable("APPVEYOR"), "True");
        public override string SkipReason => "Can't run on AppVeyor";
    }

    public class WindowsOnly : SkipCondition
    {
        public override bool ShouldSkip => !PlatformHelper.IsWindows;
        public override string SkipReason => "Can only be run on Windows";
    }

    public class DesktopRuntimeOnly : SkipCondition
    {
        public override bool ShouldSkip =>
#if NET472
            false;
#elif NETCOREAPP
            true;
#elif NETSTANDARD2_0
            throw new PlatformNotSupportedException();
#else
#error Unsupported configuration
#endif
        public override string SkipReason => "Can only be run on Desktop runtime";
    }
}
