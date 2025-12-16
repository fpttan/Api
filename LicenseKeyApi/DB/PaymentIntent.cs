public sealed class PaymentIntent
{
    public string Id { get; set; } = default!;              // "pi_xxx"
    public string LicenseKey { get; set; } = default!;
    public string PlanCode { get; set; } = default!;        // "DAY_7" | "MONTH_1"...
    public long AmountVnd { get; set; }
    public string ProviderTxnId { get; set; } = default!; // id giao dịch SePay khi đã thanh toán
    public string Channel { get; set; } = "VIETQR";        // "VIETQR" (hiển thị QR), provider xử lý tiền là SePay
    public string TransferNote { get; set; } = default!;    // "INV-XXXX"
    public DateTime ExpiresAtUtc { get; set; }
    public IntentStatus Status { get; set; } = IntentStatus.Pending;         // Pending | Succeeded | Canceled | Expired
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? OccurredAt { get; set; }               // thời điểm giao dịch thành công (nếu có)
    public string? QrUrl { get; set; }  // URL ảnh QR (img.vietqr.io)
}