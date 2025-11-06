using Newtonsoft.Json;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class ElementGroupsResponse : GraphQLResponse<ElementGroupsData> { }
    public class ElementGroupsData
    {
        [JsonProperty("elementGroupsByProject")]
        public ElementGroupsContainer ElementGroups { get; set; }
    }

    public class ElementGroupsContainer
    {
        [JsonProperty("pagination")]
        public Pagination Pagination { get; set; }

        [JsonProperty("results")]
        public List<ElementGroup> Results { get; set; }
    }

    public class ElementGroup
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("alternativeIdentifiers")]
        public AlternativeIdentifiers AlternativeIdentifiers { get; set; }
        [JsonProperty("components")]
        public ElementComponents Components { get; set; }
    }

    public class ElementComponents
    {
        [JsonProperty("results")]
        public List<ComponentResult> Results { get; set; }
    }

    public class ComponentResult
    {
        [JsonProperty("elementGroup")]
        public ElementGroup ElementGroup { get; set; }
    }



}