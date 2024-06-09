using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ClientServerGame
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            HostPanel.Visibility = Visibility.Visible;
        }

        private void OpenServerWindow(string ipAddress, int port)
        {
            ServerWindow serverWindow = new ServerWindow(ipAddress, port);
            serverWindow.Show();
        }

        private void SelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsInitialized) return;
            if (SelectionComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "Host a Server":
                        HostPanel.Visibility = Visibility.Visible;
                        ConnectPanel.Visibility = Visibility.Collapsed;
                        break;
                    case "Connect to a Server":
                        HostPanel.Visibility = Visibility.Collapsed;
                        ConnectPanel.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void HostButton_Click(object sender, RoutedEventArgs e)
        {
            string ipAddress = "127.0.0.1";
            int port = int.Parse(PortTextBox.Text);
            OpenServerWindow(ipAddress, port);
        }
    }
}