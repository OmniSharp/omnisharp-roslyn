using Newtonsoft.Json;

namespace OmniSharp.Models.V2.CodeActions
{
    public interface ICodeActionRequest
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        int Line { get; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        int Column { get; }
        string Buffer { get; }
        string FileName { get; }
        Range Selection { get; }

        ICodeActionRequest WithSelection(Range newSelection);
    }
}
