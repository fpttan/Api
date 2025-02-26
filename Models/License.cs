namespace LicenseAPI.Models
{
    public class License
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public string ExpiryDateDaily { get; set; }
        public string ExpiryDate200v { get; set; }
    }
}