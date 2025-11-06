namespace revit_aec_dm_extensibility_sample.Models
{
    /// <summary>
    /// Context information for property updates
    /// </summary>
    public class PropertyUpdateContext
    {
        public string ElementId { get; set; }
        public string DefinitionId { get; set; }
    }
}
