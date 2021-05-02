using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.Linq;

namespace OmniSharp.Mef
{
    internal class MefValueProvider<T> : ExportDescriptorProvider
    {
        private readonly T _item;
        private readonly IDictionary<string, object> _metadata;

        public MefValueProvider(T item, IDictionary<string, object> metadata)
        {
            _item = item;
            _metadata = metadata;
        }

        public override IEnumerable<ExportDescriptorPromise> GetExportDescriptors(CompositionContract contract, DependencyAccessor descriptorAccessor)
        {
            if (contract.ContractType == typeof(T))
            {
                yield return new ExportDescriptorPromise(contract, string.Empty, true,
                    () => Enumerable.Empty<CompositionDependency>(),
                    deps => ExportDescriptor.Create((context, operation) => _item, _metadata ?? new Dictionary<string, object>()));
            }
        }
    }

    internal static class MefValueProvider
    {
        public static MefValueProvider<T> From<T>(T value, IDictionary<string, object> metadata = null)
        {
            return new MefValueProvider<T>(value, metadata);
        }
    }
}
