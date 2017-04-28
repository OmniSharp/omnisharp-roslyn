namespace OmniSharp.Eventing
{
    public interface IEventEmitter
    {
        void Emit(string kind, object args);
    }
}