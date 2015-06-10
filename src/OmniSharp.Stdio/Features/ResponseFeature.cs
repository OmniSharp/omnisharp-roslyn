using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.Http;

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

        public void OnSendingHeaders(Action<object> callback, object state)
        {
            // nothing
        }
        
        public void OnResponseCompleted(Action<object> act, object state)
        {
            //nothing again
        }
    }
}
