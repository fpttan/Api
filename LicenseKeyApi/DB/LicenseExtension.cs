public sealed class LicenseExtension
{
    public string Id { get; set; } = default!;
    public string LicenseKey { get; set; } = default!;
    public string PlanCode { get; set; } = default!;
    public int DeltaDays { get; set; }
    public DateTime? OldExpireAtUtc { get; set; }
    public DateTime? NewExpireAtUtc { get; set; }
    public string SourcePaymentId { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow.AddHours(7);
}