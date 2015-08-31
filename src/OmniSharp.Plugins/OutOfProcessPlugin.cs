using System.Threading.Tasks;

namespace OmniSharp.Plugins
{
    public class OutOfProcessPlugin
    {
        public OutOfProcessPlugin(string[] supportedEndpoints)
        {
            SupportedEndpoints = supportedEndpoints;
        }

        public string[] SupportedEndpoints { get; }
        public string Language { get; set; }

        public Task<object> Handle(object Request)
        {
            return null;
        }
    }
}
