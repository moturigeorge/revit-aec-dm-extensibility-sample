using Newtonsoft.Json;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class HubsData
    {
        [JsonProperty("hubs")]
        public HubsContainer Hubs { get; set; }
    }

    public class HubsContainer
    {
        [JsonProperty("pagination")]
        public Pagination Pagination { get; set; }

        [JsonProperty("results")]
        public List<Hub> Results { get; set; }
    }

    public class Hub
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("alternativeIdentifiers")]
        public AlternativeIdentifiers AlternativeIdentifiers { get; set; }
    }

    public class HubsResponse : GraphQLResponse<HubsData> { }
}
