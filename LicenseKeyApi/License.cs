using System.ComponentModel.DataAnnotations;

public class License
{
    [Key]
    public required string LicenseKey { get; set; }
    public required string Name { get; set; }
    public required string? TimeExpireDaily { get; set; }
    public required string? TimeExpire200v { get; set; }
    public required string Multiversion {  get; set; }
    public required string? TimeExpireTool { get; set; }
}
