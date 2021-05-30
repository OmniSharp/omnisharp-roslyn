using Newtonsoft.Json;

namespace OmniSharp.Models.V2.CodeActions
{
    public interface ICodeActionRequest
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        int Column { get; set; }
        string Buffer { get; set; }
        string FileName { get; set; }
        Range Selection { get; set; }

        ICodeActionRequest WithSelection(Range newSelection);
    }
}
