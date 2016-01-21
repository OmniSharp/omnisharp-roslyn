using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Http.Internal;

namespace OmniSharp.Stdio.Features
{
    internal class ResponseFeature : IHttpResponseFeature
    {
        public ResponseFeature()
        {
            Headers = new HeaderDictionary();
            Reset();
        }

        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; }

        public Stream Body { get; set; }

        public bool HeadersSent { get; set; }

        public bool HasStarted { get { return false; } }

        public void Reset()
        {
            Headers = Headers ?? new HeaderDictionary();
            Headers.Clear();
            StatusCode = 200;
        }

        public void OnStarting(Func<object, Task> callback, object state) { }

        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
