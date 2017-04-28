namespace OmniSharp.Models
{
    public interface IAggregateResponse
    {
        IAggregateResponse Merge(IAggregateResponse response);
    }
}
