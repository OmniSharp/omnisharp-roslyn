namespace OmniSharp.Options
{
    public class FindSymbolsOptions
    {
        public const int Unlimited = -1;

        public int MinFilterLength { get; set; }
        public int MaxItemsToReturn { get; set; } = Unlimited;
    }
}
