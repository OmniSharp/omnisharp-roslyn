using System.IO;

namespace TestUtility
{
    public static class TestIO
    {
        public static string GetRandomTempFolderPath()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                Path.GetRandomFileName()
            );

            Directory.CreateDirectory(path);

            return path;
        }

        public static void TouchFakeFile(string path)
        {
            File.WriteAllText(path, "just testing :)");
        }
    }
}
