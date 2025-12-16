using System.ComponentModel.DataAnnotations;

public class License
{
    [Key]
    public required string LicenseKey { get; set; }
    public required string Name { get; set; }
    public  DateTime? TimeExpireDaily { get; set; }
    public  DateTime? TimeExpire200v { get; set; }
    public  DateTime? TimeExpireTool { get; set; }
    public  DateTime? TimeExpireAoMaThap { get; set; }
    public  DateTime? TimeExpireNoel { get; set; }
    public bool? Multiversion {  get; set; }
    public string? TypeTool { get; set; }
}
