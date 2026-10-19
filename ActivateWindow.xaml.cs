using BarcodeStudio.Services;
using System.Windows;

namespace BarcodeStudio;
public partial class ActivateWindow : Window
{
    private readonly LicenseService _license;
    public ActivateWindow(LicenseService license)
    {
        InitializeComponent();
        _license = license;
        MotherboardBox.Text = license.MotherboardSerial;
        ProcessorBox.Text = license.ProcessorId;
    }
    private void CopyMotherboard_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(MotherboardBox.Text);
    private void CopyProcessor_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(ProcessorBox.Text);
    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (_license.Activate(ActivationCodeBox.Text))
        {
            MessageBox.Show("تم تفعيل البرنامج بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        else
            MessageBox.Show("كود التفعيل غير صحيح أو تعذر قراءة بيانات الجهاز", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
