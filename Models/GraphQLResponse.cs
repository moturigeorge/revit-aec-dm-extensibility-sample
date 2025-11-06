using Newtonsoft.Json;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class GraphQLResponse<TData>
    {
        [JsonProperty("data")]
        public TData Data { get; set; }

        [JsonProperty("extensions")]
        public Extensions Extensions { get; set; }
    }
}
