using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/formatAfterKeystroke", typeof(FormatAfterKeystrokeRequest), typeof(FormatRangeResponse))]
    public class FormatAfterKeystrokeRequest : Request
    {
        public string Character { get; set; }

        public char Char { get { return string.IsNullOrEmpty(Character) ? (char)0 : Character[0]; } }
    }
}
