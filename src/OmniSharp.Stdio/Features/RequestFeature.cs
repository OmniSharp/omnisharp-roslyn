using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.HttpFeature;
using OmniSharp.Stdio.Protocol;

namespace OmniSharp.Stdio.Features
{
    internal class RequestFeature : IHttpRequestFeature
    {
        public RequestFeature()
        {
            Body = Stream.Null;
            Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            Method = "POST";
            Path = "";
            PathBase = "";
            Protocol = "HTTP/1.1";
            QueryString = "";
            Scheme = "http";
        }

        public Stream Body { get; set; }

        public IDictionary<string, string[]> Headers { get; set; }

        public string Method { get; set; }

        public string Path { get; set; }

        public string PathBase { get; set; }

        public string Protocol { get; set; }

        public string QueryString { get; set; }

        public string Scheme { get; set; }
    }
}
