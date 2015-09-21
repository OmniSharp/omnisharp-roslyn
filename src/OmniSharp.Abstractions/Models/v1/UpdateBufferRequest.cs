using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/updatebuffer", typeof(UpdateBufferRequest), typeof(object))]
    public class UpdateBufferRequest : Request
    {
        // Instead of updating the buffer from the editor,
        // set this to allow updating from disk
        public bool FromDisk { get; set; }
    }
}
