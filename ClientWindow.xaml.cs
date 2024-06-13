using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace ClientServerGame
{
    public partial class ClientWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private bool isConnected = false;
        private string playerSymbol;
        private Button[,] buttons = new Button[3, 3];

        public ClientWindow(string ipAddress, int port)
        {
            InitializeComponent();
            ConnectionInfoTextBlock.Text = $"Połączono z serwerem {ipAddress}:{port}";
            ConnectToServer(ipAddress, port);
            InitializeButtons();
        }

        private void InitializeButtons()
        {
            buttons[0, 0] = this.Button00;
            buttons[0, 1] = this.Button01;
            buttons[0, 2] = this.Button02;
            buttons[1, 0] = this.Button10;
            buttons[1, 1] = this.Button11;
            buttons[1, 2] = this.Button12;
            buttons[2, 0] = this.Button20;
            buttons[2, 1] = this.Button21;
            buttons[2, 2] = this.Button22;
        }

        private void ConnectToServer(string ipAddress, int port)
        {
            try
            {
                client = new TcpClient(ipAddress, port);
                stream = client.GetStream();
                isConnected = true;

                receiveThread = new Thread(ReceiveMessages) { IsBackground = true };
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                LogMessage($"Wyjątek: {ex.Message}");
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                while (isConnected && (bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string jsonMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    var message = JsonConvert.DeserializeObject<dynamic>(jsonMessage);
                    if (message.type == "assign")
                    {
                        playerSymbol = message.symbol;
                        LogMessage($"Przydzielony symbol: {playerSymbol}");
                    }
                    else if (message.type == "move")
                    {
                        Dispatcher.Invoke(() =>
                        {
                            int row = message.row;
                            int column = message.column;
                            string symbol = message.symbol;
                            buttons[row, column].Content = symbol;
                            CurrentTurnTextBlock.Text = $"Aktualna tura: {(symbol == "X" ? "O" : "X")}";
                        });
                    }
                    else if (message.type == "info")
                    {
                        LogMessage($"{message.content}");
                        Dispatcher.Invoke(() =>
                        {
                            if (message.content.ToString().Contains("wygrywa") || message.content.ToString().Contains("remis"))
                            {
                                WinnerInfoTextBlock.Text = message.content.ToString();
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Wyjątek: {ex.Message}");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (isConnected)
            {
                isConnected = false;
                stream.Close();
                client.Close();
                LogMessage("Rozłączono z serwerem...");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string commandParameter)
            {
                var parameters = commandParameter.Split(',');
                int row = int.Parse(parameters[0]);
                int column = int.Parse(parameters[1]);

                var message = new
                {
                    type = "move",
                    row,
                    column,
                    symbol = playerSymbol
                };

                string jsonMessage = JsonConvert.SerializeObject(message);
                byte[] buffer = Encoding.ASCII.GetBytes(jsonMessage + "\n");
                stream.Write(buffer, 0, buffer.Length);
            }
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now}: {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }
    }
}
