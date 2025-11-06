using Newtonsoft.Json;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class ElementsData
    {
        [JsonProperty("elementsByElementGroup")]
        public ElementsContainer Elements { get; set; }
    }

    public class ElementsContainer
    {
        [JsonProperty("pagination")]
        public Pagination Pagination { get; set; }

        [JsonProperty("results")]
        public List<Element> Results { get; set; }
    }

    public class Element
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("alternativeIdentifiers")]
        public AlternativeIdentifiers AlternativeIdentifiers { get; set; }

        [JsonProperty("properties")]
        public PropertyContainer Properties { get; set; }

        [JsonProperty("references")]
        public References References { get; set; }
        [JsonProperty("lastModifiedBy")]
        public LastModifiedBy LastModifiedBy { get; set; }
    }

    public class LastModifiedBy
    {
        [JsonProperty("userName")]
        public string UserName { get; set; }
    }
    public class GetElementsResponse : GraphQLResponse<ElementsData> { }

}