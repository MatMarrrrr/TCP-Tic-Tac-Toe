using System;
using System.Net;
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
        private string playerSymbol;
        private Button[,] buttons = new Button[3, 3];
        private object lockObject = new object();

        public ClientWindow(string ipAddress, int port)
        {
            InitializeComponent();
            this.ConnectionInfoTextBlock.Text = $"Connected to server {ipAddress}:{port}";
            ConnectToServer(ipAddress, port);
            InitializeButtons();
            SetButtonsEnabled(false);
        }

        private void InitializeButtons()
        {
            this.buttons[0, 0] = this.Button00;
            this.buttons[0, 1] = this.Button01;
            this.buttons[0, 2] = this.Button02;
            this.buttons[1, 0] = this.Button10;
            this.buttons[1, 1] = this.Button11;
            this.buttons[1, 2] = this.Button12;
            this.buttons[2, 0] = this.Button20;
            this.buttons[2, 1] = this.Button21;
            this.buttons[2, 2] = this.Button22;
        }

        private void ConnectToServer(string ipAddress, int port)
        {
            this.client = new TcpClient(ipAddress, port);
            this.stream = client.GetStream();

            this.receiveThread = new Thread(ReceiveMessages) { IsBackground = true };
            this.receiveThread.Start();
            LogMessage("Connected to the server successfully.");
        }

        private void ReceiveMessages()
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    var jsonMessage = JsonConvert.DeserializeObject<dynamic>(message);
                    LogMessage($"Received message: {jsonMessage.ToString().Replace("\n", "").Replace("\r", "")}");
                    string messageType = jsonMessage.type;
                    switch (messageType)
                    {
                        case "info":
                            string value = jsonMessage.value;
                            Dispatcher.Invoke(() => LogMessage(value));
                            break;
                        case "assign":
                            this.playerSymbol = jsonMessage.symbol;
                            bool turn = jsonMessage.turn;
                            Dispatcher.Invoke(() => ResetButtons());
                            Dispatcher.Invoke(() => InfoTextBlock.Text = $"Assigned symbol {this.playerSymbol}");
                            string turnText = turn ? "Your turn" : "Wait for your opponent move";
                            Dispatcher.Invoke(() => TurnTextBlock.Text = turnText);
                            Dispatcher.Invoke(() => SetButtonsEnabled(turn));
                            break;
                        case "move":
                            int row = jsonMessage.row;
                            int column = jsonMessage.column;
                            char symbol = jsonMessage.symbol;
                            Dispatcher.Invoke(() =>
                            {
                                buttons[row, column].Content = symbol;
                                SetButtonsEnabled(true);
                            });
                            Dispatcher.Invoke(() => TurnTextBlock.Text = "Your turn");
                            break;
                        case "finish":
                            string finishValue = jsonMessage.value;
                            Dispatcher.Invoke(() => SetButtonsEnabled(false));
                            Dispatcher.Invoke(() => TurnTextBlock.Text = finishValue);
                            Dispatcher.Invoke(() => { PlayAgainButton.Visibility = Visibility.Visible; });
                            break;
                        case "reset":
                            Dispatcher.Invoke(() =>
                            {
                                SetButtonsEnabled(false);
                                ResetButtons();
                                PlayAgainButton.Visibility = Visibility.Hidden;
                            });
                            break;
                    }
                }
                Dispatcher.Invoke(() => {
                    LogMessage("The server terminated the connection");
                    this.ConnectionInfoTextBlock.Text = $"Not connected to server";
                    this.InfoTextBlock.Text = null;
                    this.TurnTextBlock.Text = null;
                });

            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"Exception while receiving messages: {ex.Message}"));
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            stream.Close();
            client.Close();
        }

        private void GridButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                button.Content = playerSymbol;
                string[] parameters = tag.Split(',');
                int row = int.Parse(parameters[0]);
                int column = int.Parse(parameters[1]);

                var message = new
                {
                    type = "move",
                    row,
                    column,
                    symbol = playerSymbol
                };
                Dispatcher.Invoke(() => {SetButtonsEnabled(false);});
                Dispatcher.Invoke(() => TurnTextBlock.Text = "Wait for your oponent move");
                SendMessageToServer(message);
            }
        }

        private void PlayAgain_Click(object sender, RoutedEventArgs e)
        {
            SendMessageToServer(new {type = "playAgain"});
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now}: {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void SetButtonsEnabled(bool isEnabled)
        {
            foreach (Button button in buttons)
            {
                if (button.Content == null || !isEnabled)
                {
                    button.IsEnabled = isEnabled;
                }
            }
        }

        private void ResetButtons()
        {
            foreach (Button button in buttons)
            {
                button.Content = null;
            }
        }

        public void SendMessageToServer(object message)
        {
            LogMessage($"Sent message: {message.ToString().Replace("\n", "").Replace("\r", "")}");
            var jsonMessage = JsonConvert.SerializeObject(message);
            byte[] buffer = Encoding.ASCII.GetBytes(jsonMessage);

            lock (lockObject)
            {
                stream.Write(buffer, 0, buffer.Length);
            }
        }
    }

}
