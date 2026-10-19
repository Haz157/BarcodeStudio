using BarcodeStudio.Services;
using System;
using System.Windows;

namespace BarcodeStudio;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var license = new LicenseService();
            var status = license.CheckLicense();
            if (!status.IsActivated && status.DaysRemaining <= 0)
            {
                MessageBox.Show("انتهت النسخة التجريبية (3 أيام). يجب تفعيل البرنامج للاستمرار.", "التفعيل مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning);
                var form = new ActivateWindow(license);
                if (form.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }
            else if (!status.IsActivated)
            {
                MessageBox.Show($"النسخة التجريبية: متبقي {status.DaysRemaining} يوم/أيام تقريبًا. يمكنك التفعيل لاحقًا.", "Barcode Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            var main = new MainWindow();
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("حدث خطأ في فحص الترخيص: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
