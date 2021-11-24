using Newtonsoft.Json;
using System;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public RoslynExtensionsOptions RoslynExtensionsOptions { get; set; } = new RoslynExtensionsOptions();

        public FormattingOptions FormattingOptions { get; set; } = new FormattingOptions();

        public FileOptions FileOptions { get; set; } = new FileOptions();

        public RenameOptions RenameOptions { get; set; } = new RenameOptions();

        public ImplementTypeOptions ImplementTypeOptions { get; set; } = new ImplementTypeOptions();

        public DotNetCliOptions DotNetCliOptions { get; set; } = new DotNetCliOptions();

        public OmniSharpExtensionsOptions Plugins { get; set; } = new OmniSharpExtensionsOptions();

        public override string ToString() => JsonConvert.SerializeObject(this);

        public static void PostConfigure(OmniSharpOptions options)
        {
            options.RoslynExtensionsOptions ??= new RoslynExtensionsOptions();
            options.FormattingOptions ??= new FormattingOptions();
            options.FileOptions ??= new FileOptions();
            options.RenameOptions ??= new RenameOptions();
            options.ImplementTypeOptions ??= new ImplementTypeOptions();
            options.DotNetCliOptions ??= new DotNetCliOptions();
            options.Plugins ??= new OmniSharpExtensionsOptions();
        }
    }
}
