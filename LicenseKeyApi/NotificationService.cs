public class NotificationService
{
    public string NotificationVN { get; private set; } = "Thông báo mặc định VN";
    public string NotificationBRA { get; private set; } = "Mensagem padrão do Brasil";
    public string LinkdownVN { get; private set; } = "https://example.com/vn";
    public string LinkdownBRA { get; private set; } = "https://example.com/bra";

    // ✅ GET methods
    public string GetNotificationVN() => NotificationVN;
    public string GetNotificationBRA() => NotificationBRA;
    public string GetLinkdownVN() => LinkdownVN;
    public string GetLinkdownBRA() => LinkdownBRA;

    // ✅ UPDATE methods
    public void UpdateNotificationVN(string newMessage) => NotificationVN = newMessage;
    public void UpdateNotificationBRA(string newMessage) => NotificationBRA = newMessage;
    public void UpdateLinkdownVN(string newLink) => LinkdownVN = newLink;
    public void UpdateLinkdownBRA(string newLink) => LinkdownBRA = newLink;
}

