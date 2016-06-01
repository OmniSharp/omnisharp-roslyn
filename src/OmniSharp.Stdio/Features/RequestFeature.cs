using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace OmniSharp.Stdio.Features
{
    internal class RequestFeature : IHttpRequestFeature
    {
        private string _path;

        public RequestFeature()
        {
            Reset();
        }

        public Stream Body { get; set; }

        public IHeaderDictionary Headers { get; set; }

        public string Method { get; set; }

        public string Path
        {
            get { return _path; }
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

        public string RawTarget
        {
            get { return string.Empty; }
            set { throw new NotSupportedException(); }
        }

        public string Scheme { get; set; }

        public void Reset()
        {
            Headers = Headers ?? new HeaderDictionary();
            Headers.Clear();

            Body = Stream.Null;
            Headers.Clear();
            Protocol = "HTTP/1.1";
            Scheme = "http";
            Method = "POST";
            Path = "";
            PathBase = "";
            QueryString = "";
        }
    }
}
