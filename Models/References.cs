namespace revit_aec_dm_extensibility_sample.Models
{
    public class References
    {
        public Pagination Pagination { get; set; }
        public List<ReferenceResult> Results { get; set; }
    }

    public class ReferenceResult
    {
        public string Name { get; set; }
        public string DisplayValue { get; set; }
        public ReferenceValue Value { get; set; }
    }

    public class ReferenceValue
    {
        public string Name { get; set; }
        public ReferenceProperties Properties { get; set; }
    }

    public class ReferenceProperties
    {
        public List<PropertyResult> Results { get; set; }
    }
}
