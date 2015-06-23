namespace OmniSharp.Models.V2
{
    public interface ICodeActionRequest
    {
        int Line { get; }
        int Column { get; }
        string Buffer { get; }
        string FileName { get; }
        Range Selection { get; }
    }
}
