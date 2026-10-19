namespace BarcodeStudio.Models;
public sealed class Product
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int ShelfLifeDays { get; set; } = 7;
    public override string ToString() => $"{Code} - {Name}";
}
public sealed class PrintRecord
{
    public long Id { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string Phone { get; set; } = "";
    public DateTime ProductionDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public string PrinterName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
