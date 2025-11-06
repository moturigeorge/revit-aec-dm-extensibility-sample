using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class PropertyContainer
    {
        [JsonProperty("results")]
        public List<Property> Results { get; set; }
    }

    public class Property
    {
        public Definition Definition { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class Definition
    {
        public string Id { get; set; }
    }

    public class PropertyResult
    {
        public string Name { get; set; }
        public string DisplayValue { get; set; }
    }
}
