using Newtonsoft.Json;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class Pagination
    {
        [JsonProperty("cursor")]
        public string Cursor { get; set; }
    }

    public class Extensions
    {
        [JsonProperty("pointValue")]
        public PointValue PointValue { get; set; }
    }

    public class PointValue
    {
        [JsonProperty("requestedQueryPointValue")]
        public int RequestedQueryPointValue { get; set; }
    }

    public class AlternativeIdentifiers
    {
        [JsonProperty("fileUrn")]
        public string FileUrn { get; set; }

        [JsonProperty("fileVersionUrn")]
        public string FileVersionUrn { get; set; }

        [JsonProperty("externalElementId")]
        public string ExternalElementId { get; set; }

        [JsonProperty("revitElementId")]
        public string RevitElementId { get; set; }

        [JsonProperty("dataManagementAPIHubId")]
        public string? DataManagementAPIHubId { get; set; }
        [JsonProperty("dataManagementAPIProjectId")]
        public string? DataManagementAPIProjectId { get; set; }
    }

    public class UserInfo
    {
        [JsonProperty("userName")]
        public string UserName { get; set; }
    }

    public class PropertyUpdateBatch
    {
        public string ElementId { get; set; }
        public string Value { get; set; }
    }
}
