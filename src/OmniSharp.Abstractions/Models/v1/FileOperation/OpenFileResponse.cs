namespace OmniSharp.Models
{
    public class OpenFileResponse : FileOperationResponse
    {
        public OpenFileResponse(string fileName) : base(fileName, FileModificationType.Opened)
        {
        }
    }
}
