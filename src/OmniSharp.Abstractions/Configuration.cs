namespace OmniSharp
{
    public static class Configuration
    {
        public static bool ZeroBasedIndices = false;

        public const string RoslynVersion = "1.3.0.0";
        public const string RoslynPublicKeyToken = "31bf3856ad364e35";

        public static string GetRoslynAssemblyFullName(string name)
        {
            return $"{name}, Version={RoslynVersion}, Culture=neutral, PublicKeyToken={RoslynPublicKeyToken}";
        }
    }
}
