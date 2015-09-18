using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;

namespace OmniSharp.Stdio.Features
{
    internal class ResponseFeature : IHttpResponseFeature
    {
        public ResponseFeature()
        {
            Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            StatusCode = 200;
        }

        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public IDictionary<string, string[]> Headers { get; set; }

        public Stream Body { get; set; }

        public bool HeadersSent { get; set; }

        public bool HasStarted
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void OnStarting(Func<object, Task> callback, object state)
        {
            //nothing again
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
            //nothing again
        }
    }
}
