using Newtonsoft.Json;

namespace revit_aec_dm_extensibility_sample.Models
{
    public class ProjectsData
    {
        [JsonProperty("projects")]
        public ProjectsContainer Projects { get; set; }
    }

    public class ProjectsContainer
    {
        [JsonProperty("pagination")]
        public Pagination Pagination { get; set; }

        [JsonProperty("results")]
        public List<Project> Results { get; set; }
    }

    public class Project
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
        public AlternativeIdentifiers AlternativeIdentifiers { get; set; }
    }

    public class ProjectsResponse : GraphQLResponse<ProjectsData> { }
}
