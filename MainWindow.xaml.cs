using BarcodeStudio.Models;
using BarcodeStudio.Services;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace BarcodeStudio;
public partial class MainWindow : Window
{
    private readonly Database db = new();
    private readonly BackupService backups;
    private long editId;
    private bool loading=true;
    private bool changingExpiry;
    private List<Product> allProducts = new();
    private Product? selectedPrintProduct;
    private bool settingSearchText;
    private bool printing;
    private void VisualOptions_Changed(object sender,RoutedEventArgs e)=>UpdatePreview();
    public MainWindow()
    {
        InitializeComponent();
        backups = new BackupService(db.FilePath);
        backups.StartPeriodic(TimeSpan.FromMinutes(30));
        Closed += (_, _) => backups.Dispose();
        CompanyBox.Text=db.GetSetting("company","اسم الشركة");
        PhoneBox.Text=db.GetSetting("phone","");
        PrintCompany.Text=CompanyBox.Text;PrintPhone.Text=PhoneBox.Text;
        ProductionPicker.SelectedDate=DateTime.Today;
        ReloadProducts();ReloadHistory();loading=false;
        UpdateExpiry();UpdatePreview();
        StatusText.Text="قاعدة البيانات: "+db.FilePath;
    }
    private void ReloadProducts()
    {
        var previousCode=selectedPrintProduct?.Code;
        allProducts=db.Products();
        ProductsGrid.ItemsSource=allProducts;
        selectedPrintProduct=allProducts.FirstOrDefault(p=>p.Code==previousCode)
                             ?? (previousCode==null ? allProducts.FirstOrDefault() : null);
        ShowSelectedProduct();
    }
    private void ShowSelectedProduct()
    {
        settingSearchText=true;
        PrintProductSearch.Text=selectedPrintProduct is null ? "" : selectedPrintProduct.Name;
        PrintProductSearch.CaretIndex=PrintProductSearch.Text.Length;
        settingSearchText=false;
        ProductSuggestionsPopup.IsOpen=false;
        if(selectedPrintProduct is Product p)
        {
            PrintDays.Text=p.ShelfLifeDays.ToString();
            UpdateExpiry();
        }
        UpdatePreview();
    }
    private void SelectPrintProduct(Product product)
    {
        selectedPrintProduct=product;
        ShowSelectedProduct();
        PrintProductSearch.Focus();
        PrintProductSearch.CaretIndex=PrintProductSearch.Text.Length;
    }
    private void FilterSuggestions()
    {
        var term=PrintProductSearch.Text.Trim();
        var matching=allProducts.Where(p=>p.Name.Contains(term,StringComparison.CurrentCultureIgnoreCase)
                     || p.Code.Contains(term,StringComparison.OrdinalIgnoreCase)).Take(30).ToList();
        ProductSuggestionsList.ItemsSource=matching;
        ProductSuggestionsList.SelectedIndex=matching.Count>0 ? 0 : -1;
        ProductSuggestionsPopup.IsOpen=matching.Count>0;
    }
    private void PrintProductSearch_TextChanged(object sender,TextChangedEventArgs e)
    {
        if(settingSearchText || loading || ProductSuggestionsList==null)return;
        // No product is selected until the user confirms a suggestion.
        selectedPrintProduct=null;
        FilterSuggestions();
        UpdatePreview();
    }
    private void PrintProductSearch_GotKeyboardFocus(object sender,KeyboardFocusChangedEventArgs e)
    {
        if(!loading && !settingSearchText)FilterSuggestions();
    }
    private void PrintProductSearch_PreviewKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape){ProductSuggestionsPopup.IsOpen=false;e.Handled=true;return;}
        if(e.Key is Key.Down or Key.Up)
        {
            if(!ProductSuggestionsPopup.IsOpen)FilterSuggestions();
            if(ProductSuggestionsList.Items.Count==0)return;
            int i=ProductSuggestionsList.SelectedIndex;
            i=e.Key==Key.Down ? Math.Min(i+1,ProductSuggestionsList.Items.Count-1) : Math.Max(i-1,0);
            ProductSuggestionsList.SelectedIndex=i;
            ProductSuggestionsList.ScrollIntoView(ProductSuggestionsList.SelectedItem);
            e.Handled=true;
        }
        else if(e.Key==Key.Enter && ProductSuggestionsPopup.IsOpen)
        {
            if(ProductSuggestionsList.SelectedItem is Product p)SelectPrintProduct(p);
            e.Handled=true;
        }
    }
    private void ProductSuggestionsList_PreviewKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Enter && ProductSuggestionsList.SelectedItem is Product p)
        {SelectPrintProduct(p);e.Handled=true;}
        else if(e.Key==Key.Escape){ProductSuggestionsPopup.IsOpen=false;PrintProductSearch.Focus();e.Handled=true;}
    }
    private void ProductSuggestionsList_PreviewMouseLeftButtonUp(object sender,MouseButtonEventArgs e)
    {
        if(ProductSuggestionsList.SelectedItem is Product p)SelectPrintProduct(p);
    }
    private void ProductSuggestionsList_MouseDoubleClick(object sender,MouseButtonEventArgs e)
    {
        if(ProductSuggestionsList.SelectedItem is Product p)SelectPrintProduct(p);
    }
    private void ReloadHistory() => HistoryGrid.ItemsSource=db.Records();
    private void ProductsGrid_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(ProductsGrid.SelectedItem is not Product p)return;
        editId=p.Id;CodeBox.Text=p.Code;NameBox.Text=p.Name;PriceBox.Text=p.Price.ToString(CultureInfo.CurrentCulture);ShelfBox.Text=p.ShelfLifeDays.ToString();
    }
    private void NewProduct_Click(object sender,RoutedEventArgs e) {ProductsGrid.SelectedItem=null;editId=0;CodeBox.Clear();NameBox.Clear();PriceBox.Text="0";ShelfBox.Text="7";CodeBox.Focus();}
    private void SaveProduct_Click(object sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(CodeBox.Text)||string.IsNullOrWhiteSpace(NameBox.Text)){MessageBox.Show("أدخل كود المنتج واسمه.");return;}
        if(CodeBox.Text.Any(c=>c>127)){MessageBox.Show("استخدم أحرفًا وأرقامًا إنجليزية في كود الباركود.");return;}
        if(!decimal.TryParse(PriceBox.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out var price)||price<0 || !int.TryParse(ShelfBox.Text,out var days)||days<0||days>36500){MessageBox.Show("راجع السعر وعدد أيام الصلاحية.");return;}
        try{db.SaveProduct(new Product{Id=editId,Code=CodeBox.Text,Name=NameBox.Text,Price=price,ShelfLifeDays=days});ReloadProducts();NewProduct_Click(sender,e);StatusText.Text="تم حفظ المنتج.";}
        catch(SqliteException ex){MessageBox.Show("تعذر حفظ المنتج. تأكد أن الكود غير مكرر.\n"+ex.Message);}
    }
    private void DeleteProduct_Click(object sender,RoutedEventArgs e)
    {
        if(editId==0)return;
        if(MessageBox.Show("هل تريد حذف المنتج المحدد؟ سجل الطباعة السابق لن يُحذف.","تأكيد",MessageBoxButton.YesNo)==MessageBoxResult.Yes){db.DeleteProduct(editId);ReloadProducts();NewProduct_Click(sender,e);}
    }
    private void SaveSettings_Click(object sender,RoutedEventArgs e){db.SetSetting("company",CompanyBox.Text);db.SetSetting("phone",PhoneBox.Text);PrintCompany.Text=CompanyBox.Text;PrintPhone.Text=PhoneBox.Text;MessageBox.Show("تم حفظ الإعدادات.");}
    private void ProductionPicker_SelectedDateChanged(object sender,SelectionChangedEventArgs e){UpdateExpiry();UpdatePreview();}
    private void ExpiryPicker_SelectedDateChanged(object sender,SelectionChangedEventArgs e){if(!changingExpiry && !loading && AutoExpiryCheck!=null && AutoExpiryCheck.IsChecked==true)AutoExpiryCheck.IsChecked=false;UpdatePreview();}
    private void AutoExpiryCheck_Changed(object sender,RoutedEventArgs e){UpdateExpiry();UpdatePreview();}
    private void Preview_Changed(object sender,TextChangedEventArgs e){UpdateExpiry();UpdatePreview();}
    private void RefreshPreview_Click(object sender,RoutedEventArgs e)=>UpdatePreview();
    private void UpdateExpiry()
    {
        if(loading || AutoExpiryCheck?.IsChecked!=true || ProductionPicker?.SelectedDate is not DateTime prod || !int.TryParse(PrintDays?.Text,out var days) || days<0||days>36500)return;
        try{changingExpiry=true;ExpiryPicker.SelectedDate=prod.AddDays(days);}catch(ArgumentOutOfRangeException){}finally{changingExpiry=false;}
    }
    private bool TryGetPrintData(out Product product,out DateTime production,out DateTime expiry,out int days,out double width,out double height)
    {
        product=selectedPrintProduct!;production=ProductionPicker.SelectedDate??default;expiry=ExpiryPicker.SelectedDate??default;
        days=0;width=60;height=40;
        return product!=null && !string.IsNullOrWhiteSpace(product.Code) && production!=default && expiry!=default && expiry>=production &&
               int.TryParse(PrintDays.Text,out days)&&days>=0&&days<=36500&&
               double.TryParse(WidthBox.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out width)&&width>=20&&width<=200&&
               double.TryParse(HeightBox.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out height)&&height>=15&&height<=200;
    }
    private void UpdatePreview()
    {
        if(loading||LabelPreview==null)return;
        if(TryGetPrintData(out var p,out var prod,out var exp,out var days,out var w,out var h))
        {
            try{LabelPreview.Content=LabelFactory.Build(p,PrintCompany.Text,PrintPhone.Text,prod,exp,days,w,h,ShowExpiryCheck.IsChecked==true,ProductFontSlider.Value);}
            catch(Exception){LabelPreview.Content=new TextBlock{Text="تعذر عرض الباركود. راجع كود المنتج.",Foreground=Brushes.DarkRed};}
        }
        else LabelPreview.Content=new TextBlock{Text="اختر المنتج وأدخل بيانات صحيحة للمعاينة.",Foreground=Brushes.DarkRed};
    }
    private async void Print_Click(object sender,RoutedEventArgs e)
    {
        if(printing)return;
        if(!TryGetPrintData(out var p,out var production,out var expiry,out var days,out var width,out var height))
        {MessageBox.Show("راجع المنتج والتواريخ والمقاسات.");return;}
        if(!int.TryParse(CopiesBox.Text,out var quantity)||quantity<1||quantity>1000)
        {MessageBox.Show("عدد النسخ يجب أن يكون بين 1 و1000.");return;}
        var dialog=new PrintDialog();
        if(dialog.ShowDialog()!=true)return;
        // Capture immutable values on the UI thread; create all WPF visuals on the worker STA.
        var printer=dialog.PrintQueue?.FullName;
        if(string.IsNullOrWhiteSpace(printer)){MessageBox.Show("لم يتم اختيار طابعة.");return;}
        byte[]? ticketBytes=null;
        if(dialog.PrintTicket!=null)
        {
            using var stream=new MemoryStream();
            dialog.PrintTicket.SaveTo(stream);
            ticketBytes=stream.ToArray();
        }
        var snapshot=new Product {Code=p.Code,Name=p.Name,Price=p.Price,ShelfLifeDays=p.ShelfLifeDays};
        var company=PrintCompany.Text;var phone=PrintPhone.Text;
        var showExpiry=ShowExpiryCheck.IsChecked==true;var fontSize=ProductFontSlider.Value;
        var record=new PrintRecord{ProductName=p.Name,ProductCode=p.Code,CompanyName=company,Phone=phone,ProductionDate=production,ExpiryDate=expiry,Quantity=quantity,PrinterName=printer,Status="قيد التجهيز",CreatedAt=DateTime.Now};
        printing=true;PrintButton.IsEnabled=false;TaskProgress.Value=0;ProgressPercentage.Text="0%";
        StatusText.Text="جاري تجهيز الملصقات وإرسالها للطابعة في الخلفية...";
        // Invoke on Dispatcher so UI progress remains responsive even while spooling.
        var progress=new Progress<int>(percent=>{TaskProgress.Value=percent;ProgressPercentage.Text=$"{percent}%";StatusText.Text=percent<90?$"تجهيز الملصقات: {percent}%":percent<100?"جاري إرسال المهمة إلى Windows Print Spooler...":"تم إرسال المهمة لقائمة انتظار الطباعة.";});
        try
        {
            await PrintOnStaThreadAsync(printer,ticketBytes,snapshot,company,phone,production,expiry,days,width,height,quantity,showExpiry,fontSize,progress);
            record.Status="أُرسلت للطابعة";
            StatusText.Text=$"تم إرسال {quantity} ملصق إلى {printer} (قد تكون الطابعة ما زالت تعمل).";
        }
        catch(Exception ex)
        {
            record.Status="فشل الإرسال";
            StatusText.Text="تعذّر إرسال مهمة الطباعة.";
            MessageBox.Show("خطأ أثناء إرسال الطباعة:\n"+ex.Message,"خطأ",MessageBoxButton.OK,MessageBoxImage.Error);
        }
        finally
        {
            try
            {
                db.AddRecord(record);
                ReloadHistory();
                // Fire-and-observe: backup runs independently; do not hold the print button disabled.
                _ = BackupAfterPrintAsync();
            }
            catch(Exception ex){MessageBox.Show("تعذّر حفظ سجل الطباعة: "+ex.Message);}
            printing=false;PrintButton.IsEnabled=true;
        }
    }

    private async Task BackupAfterPrintAsync()
    {
        try
        {
            var result = await backups.RunAsync();
            if (result.Errors.Count > 0 && IsLoaded)
                StatusText.Text += $" | النسخ الاحتياطي: نجح على {result.SuccessfulPaths.Count} بارتشن؛ تعذر على {result.Errors.Count}.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (IsLoaded) StatusText.Text += " | تعذّر النسخ الاحتياطي: " + ex.Message;
        }
    }

    private static Task PrintOnStaThreadAsync(string printerName,byte[]? ticketBytes,Product product,
        string company,string phone,DateTime production,DateTime expiry,int days,
        double width,double height,int quantity,bool showExpiry,double fontSize,IProgress<int> progress)
    {
        var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                using var server=new LocalPrintServer();
                using var queue=server.GetPrintQueue(printerName);
                var dialog=new PrintDialog{PrintQueue=queue};
                if(ticketBytes!=null)
                {
                    using var stream=new MemoryStream(ticketBytes);
                    dialog.PrintTicket=new System.Printing.PrintTicket(stream);
                }
                var document=new FixedDocument();
                var w=width*96/25.4;var h=height*96/25.4;
                document.DocumentPaginator.PageSize=new Size(w,h);
                for(var i=0;i<quantity;i++)
                {
                    var page=new FixedPage{Width=w,Height=h,Background=Brushes.White};
                    var label=LabelFactory.Build(product,company,phone,production,expiry,days,width,height,showExpiry,fontSize);
                    page.Children.Add(label);FixedPage.SetLeft(label,0);FixedPage.SetTop(label,0);
                    var content=new PageContent();
                    ((System.Windows.Markup.IAddChild)content).AddChild(page);
                    document.Pages.Add(content);
                    if(i==quantity-1 || i%10==0)progress.Report((int)((i+1)*90.0/quantity));
                }
                progress.Report(95);
                dialog.PrintDocument(document.DocumentPaginator,$"ملصقات {product.Name}");
                progress.Report(100); // Windows accepted the job; not a physical-print completion signal.
                completion.TrySetResult(true);
            }
            catch(Exception ex){completion.TrySetException(ex);}
        });
        thread.IsBackground=true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
    private void Reprint_Click(object sender,RoutedEventArgs e)
    {
        if(HistoryGrid.SelectedItem is not PrintRecord r){MessageBox.Show("حدد عملية من السجل أولًا.");return;}
        var prod=allProducts.FirstOrDefault(p=>p.Code==r.ProductCode);
        if(prod==null){MessageBox.Show("المنتج لم يعد موجودًا. أعد تسجيله بنفس الكود أولًا.");return;}
        SelectPrintProduct(prod);PrintCompany.Text=r.CompanyName;PrintPhone.Text=r.Phone;
        AutoExpiryCheck.IsChecked=false;ProductionPicker.SelectedDate=r.ProductionDate;ExpiryPicker.SelectedDate=r.ExpiryDate;
        PrintDays.Text=((int)(r.ExpiryDate-r.ProductionDate).TotalDays).ToString();CopiesBox.Text=r.Quantity.ToString();
        UpdatePreview();StatusText.Text="تم تجهيز بيانات العملية السابقة. انتقل لتبويب الطباعة وراجع البيانات قبل الإرسال.";
        MessageBox.Show("تم تجهيز بيانات العملية السابقة. انتقل إلى تبويب الطباعة وراجعها قبل الطباعة.");
    }
    private void Refresh_Click(object sender,RoutedEventArgs e)=>ReloadProducts();
    private void RefreshHistory_Click(object sender,RoutedEventArgs e)=>ReloadHistory();
}
