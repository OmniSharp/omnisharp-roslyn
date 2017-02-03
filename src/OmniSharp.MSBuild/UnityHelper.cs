using System;
using System.Security.Cryptography;
using System.Text;

namespace OmniSharp.MSBuild
{
    public static class UnityHelper
    {
        public static Guid GetProjectTypeGuid(string projectName)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(projectName);
                var hash = md5.ComputeHash(bytes);

                var bigEndianHash = new[] {
                    hash[3], hash[2], hash[1], hash[0],
                    hash[5], hash[4],
                    hash[7], hash[6],
                    hash[8], hash[9], hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]
                };

                return new Guid(bigEndianHash);
            }
        }

        public static bool IsUnityProject(string projectName, Guid projectTypeGuid)
        {
            return GetProjectTypeGuid(projectName) == projectTypeGuid;
        }
    }
}
