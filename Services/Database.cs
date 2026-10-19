using BarcodeStudio.Models;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Globalization;

namespace BarcodeStudio.Services;
public sealed class Database
{

	public string FilePath { get; } = GetDatabasePath();

	private static string GetDatabasePath()
	{
		string folder;

		// لو بارتشن D موجود ومتاح للكتابة
		if (Directory.Exists(@"D:\"))
		{
			try
			{
				folder = @"D:\BarcodeStudio";
				Directory.CreateDirectory(folder);

				// التأكد من صلاحية الكتابة
				string testFile = Path.Combine(
					folder, Guid.NewGuid() + ".tmp");

				using (File.Create(testFile)) { }
				File.Delete(testFile);

				return Path.Combine(folder, "barcodes.db");
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}

		// لو D مش موجود أو غير متاح للكتابة
		folder = Path.Combine(
			Environment.GetFolderPath(
				Environment.SpecialFolder.LocalApplicationData),
			"BarcodeStudio");

		Directory.CreateDirectory(folder);

		return Path.Combine(folder, "barcodes.db");
	}
	private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = FilePath }.ToString();
    private SqliteConnection Open() { var c = new SqliteConnection(ConnectionString); c.Open(); return c; }
    public Database()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var c=Open();
        using var cmd=c.CreateCommand();
        cmd.CommandText="""
        CREATE TABLE IF NOT EXISTS Products (Id INTEGER PRIMARY KEY AUTOINCREMENT, Code TEXT NOT NULL UNIQUE, Name TEXT NOT NULL, Price REAL NOT NULL DEFAULT 0, ShelfLifeDays INTEGER NOT NULL DEFAULT 7);
        CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS PrintRecords (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductName TEXT NOT NULL, ProductCode TEXT NOT NULL, CompanyName TEXT NOT NULL, Phone TEXT NOT NULL, ProductionDate TEXT NOT NULL, ExpiryDate TEXT NOT NULL, Quantity INTEGER NOT NULL, PrinterName TEXT NOT NULL, Status TEXT NOT NULL, CreatedAt TEXT NOT NULL);
        """;
        cmd.ExecuteNonQuery();
    }
    public List<Product> Products()
    {
        var result=new List<Product>(); using var c=Open(); using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT Id,Code,Name,Price,ShelfLifeDays FROM Products ORDER BY Name";
        using var r=cmd.ExecuteReader(); while(r.Read())result.Add(new Product { Id=r.GetInt64(0),Code=r.GetString(1),Name=r.GetString(2),Price=Convert.ToDecimal(r.GetDouble(3)),ShelfLifeDays=r.GetInt32(4)});
        return result;
    }
    public void SaveProduct(Product p)
    {
        using var c=Open(); using var cmd=c.CreateCommand();
        cmd.CommandText=p.Id==0 ? "INSERT INTO Products(Code,Name,Price,ShelfLifeDays) VALUES($code,$name,$price,$days)" : "UPDATE Products SET Code=$code,Name=$name,Price=$price,ShelfLifeDays=$days WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id",p.Id); cmd.Parameters.AddWithValue("$code",p.Code.Trim());cmd.Parameters.AddWithValue("$name",p.Name.Trim());cmd.Parameters.AddWithValue("$price",p.Price);cmd.Parameters.AddWithValue("$days",p.ShelfLifeDays);cmd.ExecuteNonQuery();
    }
    public void DeleteProduct(long id) {using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM Products WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();}
    public string GetSetting(string key,string fallback="") {using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Value FROM Settings WHERE Key=$key";cmd.Parameters.AddWithValue("$key",key);return cmd.ExecuteScalar()?.ToString()??fallback;}
    public void SetSetting(string key,string value) {using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO Settings(Key,Value) VALUES($key,$value) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value";cmd.Parameters.AddWithValue("$key",key);cmd.Parameters.AddWithValue("$value",value);cmd.ExecuteNonQuery();}
    public void AddRecord(PrintRecord x)
    {
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO PrintRecords(ProductName,ProductCode,CompanyName,Phone,ProductionDate,ExpiryDate,Quantity,PrinterName,Status,CreatedAt) VALUES($name,$code,$company,$phone,$prod,$expiry,$qty,$printer,$status,$created)";
        cmd.Parameters.AddWithValue("$name",x.ProductName);cmd.Parameters.AddWithValue("$code",x.ProductCode);cmd.Parameters.AddWithValue("$company",x.CompanyName);cmd.Parameters.AddWithValue("$phone",x.Phone);cmd.Parameters.AddWithValue("$prod",x.ProductionDate.ToString("O"));cmd.Parameters.AddWithValue("$expiry",x.ExpiryDate.ToString("O"));cmd.Parameters.AddWithValue("$qty",x.Quantity);cmd.Parameters.AddWithValue("$printer",x.PrinterName);cmd.Parameters.AddWithValue("$status",x.Status);cmd.Parameters.AddWithValue("$created",x.CreatedAt.ToString("O"));cmd.ExecuteNonQuery();
    }
    public List<PrintRecord> Records()
    {
        var result=new List<PrintRecord>(); using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Id,ProductName,ProductCode,CompanyName,Phone,ProductionDate,ExpiryDate,Quantity,PrinterName,Status,CreatedAt FROM PrintRecords ORDER BY Id DESC LIMIT 1000";
        using var r=cmd.ExecuteReader();while(r.Read())result.Add(new PrintRecord{Id=r.GetInt64(0),ProductName=r.GetString(1),ProductCode=r.GetString(2),CompanyName=r.GetString(3),Phone=r.GetString(4),ProductionDate=DateTime.Parse(r.GetString(5),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),ExpiryDate=DateTime.Parse(r.GetString(6),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Quantity=r.GetInt32(7),PrinterName=r.GetString(8),Status=r.GetString(9),CreatedAt=DateTime.Parse(r.GetString(10),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind)});return result;
    }
}
