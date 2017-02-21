public static class PathHelper
{
    public static string Combine(params string[] paths) =>
        System.IO.Path.Combine(paths);

    public static string GetFullPath(string path) =>
        System.IO.Path.Combine(path);
}

string CombinePaths(params string[] paths)
{
    return PathHelper.Combine(paths);
}