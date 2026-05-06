using System.Diagnostics;
using System.Windows;

namespace HerramientasSICAR.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BuyMeCoffeeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://buymeacoffee.com/nsuarez75",
                    UseShellExecute = true
                });
            }
            catch
            {
                // If it fails, silently ignore
            }
        }
    }
}
