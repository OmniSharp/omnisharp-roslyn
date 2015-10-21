using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.Linq;

namespace OmniSharp.Mef
{
    public class MefValueProvider<T> : ExportDescriptorProvider
    {
        private readonly T _item;

        public MefValueProvider(T item)
        {
            _item = item;
        }

        public override IEnumerable<ExportDescriptorPromise> GetExportDescriptors(CompositionContract contract, DependencyAccessor descriptorAccessor)
        {
            if (contract.ContractType == typeof(T))
            {
                yield return new ExportDescriptorPromise(contract, string.Empty, true,
                    () => Enumerable.Empty<CompositionDependency>(),
                    deps => ExportDescriptor.Create((context, operation) => _item, new Dictionary<string, object>()));
            }
        }
    }

    public static class MefValueProvider
    {
        public static MefValueProvider<T> From<T>(T value)
        {
            return new MefValueProvider<T>(value);
        }
    }
}
