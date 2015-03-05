namespace OmniSharp.Services
{
    public interface IEventEmitter
    {
        void Emit(string kind, object args);
    }
}