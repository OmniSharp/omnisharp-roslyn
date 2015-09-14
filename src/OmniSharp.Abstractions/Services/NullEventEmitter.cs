namespace OmniSharp.Services
{
    public class NullEventEmitter : IEventEmitter
    {
        public void Emit(string kind, object args)
        {
            // nothing
        }
    }
}