using System;
using System.Threading.Tasks;

namespace OmniSharp.Endpoint.Exports
{
    abstract class ExportHandler<TRequest, TResponse> : IComparable<ExportHandler<TRequest, TResponse>>
    {
        protected ExportHandler(string language)
        {
            Language = language;
        }

        public string Language { get; }

        public abstract int CompareTo(ExportHandler<TRequest, TResponse> other);
        public abstract Task<TResponse> Handle(TRequest request);
    }
}
