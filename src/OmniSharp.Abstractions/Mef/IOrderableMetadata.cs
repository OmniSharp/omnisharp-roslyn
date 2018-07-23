using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniSharp.Mef
{
    internal interface IOrderableMetadata
    {
        [DefaultValue("")]
        string After { get; }
        [DefaultValue("")]
        string Before { get; }
        string Name { get; }
    }

    class OrderableMetadata
    {
        public string After { get; set; }

        public string Before { get; set; }
        public string Name { get; set; }
    }
}
