namespace OmniSharp.Options
{
    public class AspNet5Options
    {
        public string Alias { get; set; }
        public string Projects { get; set; }
        public bool EnablePackageRestore { get; set; }
        public int PackageRestoreTimeout { get; set; }
    }
}