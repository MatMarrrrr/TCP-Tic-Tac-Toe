using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;

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

        private void OpenClientWindow(string ipAddress, int port)
        {
            ClientWindow clientWindow = new ClientWindow(ipAddress, port);
            clientWindow.Show();
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

        private void Host_Click(object sender, RoutedEventArgs e)
        {
            string ipAddress = "127.0.0.1";
            int port = int.Parse(PortTextBox.Text);

            if (!IsPortAvailable(port))
            {
                MessageBox.Show("Port is already in use. Please choose a different port.", "Port Unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenServerWindow(ipAddress, port);
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            string ipAddress = ServerIPTextBox.Text;
            int port = int.Parse(ConnectPortTextBox.Text);

            if (!IsServerAvailable(port))
            {
                MessageBox.Show("Cannot connect to the server on the specified port. Please check the port and try again.", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenClientWindow(ipAddress, port);
        }

        private bool IsPortAvailable(int port)
        {
            bool isAvailable = true;

            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
                listener.Start();
            }
            catch (SocketException)
            {
                isAvailable = false;
            }
            finally
            {
                if (listener != null)
                {
                    listener.Stop();
                }
            }

            return isAvailable;
        }

        private bool IsServerAvailable(int port)
        {
            bool isAvailable = true;

            try
            {
                TcpClient client = new TcpClient("127.0.0.1", port);
            }
            catch (SocketException)
            {
                isAvailable = false;
            }

            return isAvailable;
        }
    }
}