using System;
using System.ComponentModel.DataAnnotations;

public class License
{
    [Key]
    public string Key { get; set; } = string.Empty; // License Key (Primary Key)
    public string Name { get; set; } = string.Empty; // Tên License
    public DateTime ExpiryDateDaily { get; set; } // Ngày hết hạn Daily
    public DateTime ExpiryDate200v { get; set; } // Ngày hết hạn 200v
}