using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;

namespace OmniSharp.Stdio.Features
{
    internal class RequestFeature : IHttpRequestFeature
    {
        private string _path;

        public RequestFeature()
        {
            Body = Stream.Null;
            Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            Protocol = "HTTP/1.1";
            Scheme = "http";
            Method = "POST";
            Path = "";
            PathBase = "";
            QueryString = "";
        }

        public Stream Body { get; set; }

        public IDictionary<string, string[]> Headers { get; set; }

        public string Method { get; set; }

        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _path = value;
                }
                else if (value[0] != '/')
                {
                    _path = "/" + value;
                }
                else
                {
                    _path = value;
                }
            }
        }

        public string PathBase { get; set; }

        public string Protocol { get; set; }

        public string QueryString { get; set; }

        public string Scheme { get; set; }
    }
}
