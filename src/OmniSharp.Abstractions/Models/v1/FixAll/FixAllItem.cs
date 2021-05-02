namespace OmniSharp.Abstractions.Models.V1.FixAll
{
    public class FixAllItem
    {
        public FixAllItem() {}
        public FixAllItem(string id, string message)
        {
            Id = id;
            Message = message;
        }

        public string Id { get; set; }
        public string Message { get; set; }
    }
}