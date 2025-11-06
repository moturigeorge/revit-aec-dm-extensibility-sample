using Newtonsoft.Json;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class UpdateExtensionResponse
    {
        [JsonProperty("data")]
        public ResponseData Data { get; set; }

        [JsonProperty("extensions")]
        public ResponseExtensions Extensions { get; set; }
    }

    public class ResponseData
    {
        [JsonProperty("updateExtensionPropertiesOnElements")]
        public MutationResult UpdateExtensionPropertiesOnElements { get; set; }
    }

    public class MutationResult
    {
        [JsonProperty("elements")]
        public List<Element> Elements { get; set; }
    }

    public class ResponseExtensions
    {
        [JsonProperty("pointValue")]
        public PointValue PointValue { get; set; }
    }
}
