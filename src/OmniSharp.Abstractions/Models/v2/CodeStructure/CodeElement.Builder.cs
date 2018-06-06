using System.Collections.Immutable;

namespace OmniSharp.Models.V2.CodeStructure
{
    public partial class CodeElement
    {
        public class Builder
        {
            private ImmutableList<CodeElement>.Builder _childrenBuilder;
            private ImmutableDictionary<string, Range>.Builder _rangesBuilder;
            private ImmutableDictionary<string, object>.Builder _propertiesBuilder;

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

            public void AddRange(string name, Range range)
            {
                if (_rangesBuilder == null)
                {
                    _rangesBuilder = ImmutableDictionary.CreateBuilder<string, Range>();
                }

                _rangesBuilder.Add(name, range);
            }

            public void AddProperty(string name, object value)
            {
                if (_propertiesBuilder == null)
                {
                    _propertiesBuilder = ImmutableDictionary.CreateBuilder<string, object>();
                }

                _propertiesBuilder.Add(name, value);
            }

            public CodeElement ToCodeElement()
            {
                return new CodeElement(
                    Kind, Name, DisplayName,
                    _childrenBuilder?.ToImmutable(),
                    _rangesBuilder?.ToImmutable(),
                    _propertiesBuilder?.ToImmutable());
            }
        }
    }
}
