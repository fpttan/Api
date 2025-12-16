public sealed class Payment
{
    public string Id { get; set; } = default!;              // "pay_xxx"
    public string? IntentId { get; set; }
    public string ReferenceCode { get; set; } = default!;
    public long AmountVnd { get; set; }
    public string Provider { get; set; } = "SePay";         // nguồn webhook thực nhận
    public string? ExternalId { get; set; }                 // id giao dịch SePay (payload.id)
    public DateTime PaidAtUtc { get; set; }
    public IntentStatus Status { get; set; } = IntentStatus.Succeeded;
    public string? PaymentNote { get; set; } 
}