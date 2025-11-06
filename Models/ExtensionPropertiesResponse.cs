using Newtonsoft.Json;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class ExtensionPropertiesData
    {
        [JsonProperty("associatedElementsByElements")]
        public AssociatedElementsContainer AssociatedElements { get; set; }
    }

    public class AssociatedElementsContainer
    {
        [JsonProperty("results")]
        public List<ExtendedElement> Results { get; set; }
    }

    public class ExtendedElement
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("createdBy")]
        public UserInfo CreatedBy { get; set; }

        [JsonProperty("lastModifiedBy")]
        public UserInfo LastModifiedBy { get; set; }

        [JsonProperty("properties")]
        public PropertyContainer Properties { get; set; }
        [JsonProperty("components")]
        public Components components { get; set; }
    }
    public class Components
    {
        [JsonProperty("results")]
        public List<Results> results { get; set; }
    }
    public class Results
    {
        [JsonProperty("id")]
        public string? id { get; set; }

        [JsonProperty("name")]
        public string? name { get; set; }

        [JsonProperty("createdBy")]
        public UserInfo createdBy { get; set; }

        [JsonProperty("lastModifiedBy")]
        public UserInfo lastModifiedBy { get; set; }

        [JsonProperty("components")]
        public Components components { get; set; }

        [JsonProperty("references")]
        public References references { get; set; }

        //[JsonProperty("properties")]
        //public Properties properties { get; set; }

        [JsonProperty("elementGroup")]
        public ElementGroup elementGroup { get; set; }

        [JsonProperty("element")]
        public Element element { get; set; }

        [JsonProperty("value")]
        public object value { get; set; }

        [JsonProperty("definition")]
        public Definition definition { get; set; }
    }

    public class ExtensionPropertiesResponse : GraphQLResponse<ExtensionPropertiesData> { }
}
