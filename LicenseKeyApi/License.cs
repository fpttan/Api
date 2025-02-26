using System.ComponentModel.DataAnnotations;

public class License
{
    [Key]
    public string LicenseKey { get; set; }
    public string Name { get; set; }
    public string TimeExpireDaily { get; set; }
    public string TimeExpire200v { get; set; }
}
