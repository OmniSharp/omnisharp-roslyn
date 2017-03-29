using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;

namespace OmniSharp.DotNetTest
{
    internal static class MessageExtensions
    {
        public static T DeserializePayload<T>(this Message message)
        {
            return JsonDataSerializer.Instance.DeserializePayload<T>(message);
        }
    }
}
