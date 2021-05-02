namespace OmniSharp.Options
{
    public class ImplementTypeOptions
    {
        public ImplementTypeInsertionBehavior? InsertionBehavior { get; set; }
        public ImplementTypePropertyGenerationBehavior? PropertyGenerationBehavior { get; set; }
    }

    public enum ImplementTypeInsertionBehavior
    {
        WithOtherMembersOfTheSameKind = 0,
        AtTheEnd = 1,
    }

    public enum ImplementTypePropertyGenerationBehavior
    {
        PreferThrowingProperties = 0,
        PreferAutoProperties = 1,
    }
}
