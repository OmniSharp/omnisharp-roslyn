using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniSharp.Models
{
    public class HashedString
    {
        public HashedString(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}
