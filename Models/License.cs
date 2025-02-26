using System.ComponentModel.DataAnnotations;

namespace LicenseServer.Models
{
    public class License
    {
        [Key]
        public string Key { get; set; }
        public string Name { get; set; }
        public string ExpiryDateDaily { get; set; }
        public string ExpiryDate200v { get; set; }
    }
}