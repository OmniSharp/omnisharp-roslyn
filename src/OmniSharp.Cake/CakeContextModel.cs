namespace OmniSharp.Cake
{
    internal class CakeContextModel
    {
        public CakeContextModel(string filePath)
        {
            Path = filePath;
        }

        public string Path { get; }
    }
}