using System.Collections.Immutable;

namespace OmniSharp.Models.V2.CodeStructure
{
    public partial class CodeElement
    {
        public class Builder
        {
            private ImmutableList<CodeElement>.Builder _childrenBuilder;
            private ImmutableList<CodeElementRange>.Builder _rangeBuilder;
            private ImmutableDictionary<string, object>.Builder _propertyBuilder;

            public string Kind { get; set; }
            public string Name { get; set; }
            public string DisplayName { get; set; }

            public void AddChild(CodeElement element)
            {
                if (_childrenBuilder == null)
                {
                    _childrenBuilder = ImmutableList.CreateBuilder<CodeElement>();
                }

                _childrenBuilder.Add(element);
            }

            public void AddRange(CodeElementRange range)
            {
                if (_rangeBuilder == null)
                {
                    _rangeBuilder = ImmutableList.CreateBuilder<CodeElementRange>();
                }

                _rangeBuilder.Add(range);
            }

            public void AddProperty(string name, object value)
            {
                if (_propertyBuilder == null)
                {
                    _propertyBuilder = ImmutableDictionary.CreateBuilder<string, object>();
                }

                _propertyBuilder.Add(name, value);
            }

            public CodeElement ToCodeElement()
            {
                return new CodeElement(
                    Kind, Name, DisplayName,
                    _childrenBuilder?.ToImmutable(),
                    _rangeBuilder?.ToImmutable(),
                    _propertyBuilder?.ToImmutable());
            }
        }
    }
}
