namespace OmniSharp.Models
{
    public class RenamedFileResponse : FileOperationResponse
    {
        public RenamedFileResponse(string fileName, string newFileName)
            : base(fileName, FileModificationType.Renamed)
        {
            NewFileName = newFileName;
        }

        public string NewFileName { get; }
    }
}
