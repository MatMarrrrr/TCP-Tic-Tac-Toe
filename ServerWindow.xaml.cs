using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;

namespace ClientServerGame
{
    public partial class ServerWindow : Window
    {
        private TcpListener server;
        private Thread serverThread;
        private bool isRunning = false;
        private List<TcpClient> clients = new List<TcpClient>();
        private readonly object lockObj = new object();
        private char[,] board = new char[3, 3];
        private TcpClient player1;
        private TcpClient player2;
        private TcpClient currentPlayer;

        public ServerWindow(string ipAddress, int port)
        {
            InitializeComponent();
            IPTextBlock.Text = ipAddress;
            PortTextBlock.Text = port.ToString();
            StartServer();
        }

        private void StartServer()
        {
            string ipAddress = IPTextBlock.Text;
            int port = int.Parse(PortTextBlock.Text);

            server = new TcpListener(IPAddress.Parse(ipAddress), port);
            server.Start();

            serverThread = new Thread(ServerLoop) { IsBackground = true };
            serverThread.Start();

            isRunning = true;
            LogMessage("Server started...");
        }

        private void StopServer()
        {
            lock (lockObj)
            {
                isRunning = false;
                server.Stop();
                foreach (var client in clients)
                {
                    client.Close();
                }
                clients.Clear();
            }
            if (serverThread != null && serverThread.IsAlive)
            {
                serverThread.Join();
            }
            LogMessage("Server stopped...");
        }

        private void ServerLoop()
        {
            try
            {
                while (true)
                {
                    TcpClient client = null;
                    lock (lockObj)
                    {
                        if (!isRunning)
                        {
                            break;
                        }

                        if (server.Pending())
                        {
                            client = server.AcceptTcpClient();
                        }
                    }

                    if (client != null)
                    {
                        lock (lockObj)
                        {
                            if (clients.Count >= 2)
                            {
                                client.Close();
                                continue;
                            }

                            clients.Add(client);
                            LogMessage("Client connected...");

                            if (clients.Count == 2)
                            {
                                player1 = clients[0];
                                player2 = clients[1];
                                currentPlayer = player1;
                                StartGame();
                            }
                            else
                            {
                                Thread clientThread = new Thread(() => HandleWaitingClient(client)) { IsBackground = true };
                                clientThread.Start();
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch (SocketException ex)
            {
                if (isRunning)
                {
                    LogMessage($"SocketException: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.Message}");
            }
        }

        private void StartGame()
        {
            Thread player1Thread = new Thread(() => HandleClient(player1, 'X')) { IsBackground = true };
            Thread player2Thread = new Thread(() => HandleClient(player2, 'O')) { IsBackground = true };
            player1Thread.Start();
            player2Thread.Start();

            SendAssignMessage(player1, 'X');
            SendAssignMessage(player2, 'O');

            LogMessage("Game started between two players.");
        }

        private void HandleWaitingClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                var message = new { type = "info", content = "Waiting for another player..." };
                string jsonMessage = JsonConvert.SerializeObject(message);
                byte[] buffer = Encoding.ASCII.GetBytes(jsonMessage + "\n");
                stream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.Message}");
            }
        }

        private void HandleClient(TcpClient client, char symbol)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;

                while (true)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    string jsonMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    var message = JsonConvert.DeserializeObject<dynamic>(jsonMessage);

                    if (message.type == "move")
                    {
                        int row = message.row;
                        int column = message.column;

                        lock (lockObj)
                        {
                            if (client == currentPlayer && board[row, column] == '\0')
                            {
                                board[row, column] = symbol;
                                SendMoveMessage(player1, row, column, symbol);
                                SendMoveMessage(player2, row, column, symbol);

                                if (CheckWin(symbol))
                                {
                                    SendInfoMessage(player1, $"Player {symbol} wins!");
                                    SendInfoMessage(player2, $"Player {symbol} wins!");
                                    ResetGame();
                                }
                                else if (CheckDraw())
                                {
                                    SendInfoMessage(player1, "It's a draw!");
                                    SendInfoMessage(player2, "It's a draw!");
                                    ResetGame();
                                }
                                else
                                {
                                    currentPlayer = currentPlayer == player1 ? player2 : player1;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.Message}");
            }
        }

        private void SendAssignMessage(TcpClient client, char symbol)
        {
            NetworkStream stream = client.GetStream();
            var message = new { type = "assign", symbol = symbol };
            string jsonMessage = JsonConvert.SerializeObject(message);
            byte[] buffer = Encoding.ASCII.GetBytes(jsonMessage + "\n");
            stream.Write(buffer, 0, buffer.Length);
        }

        private void SendMoveMessage(TcpClient client, int row, int column, char symbol)
        {
            NetworkStream stream = client.GetStream();
            var message = new { type = "move", row = row, column = column, symbol = symbol };
            string jsonMessage = JsonConvert.SerializeObject(message);
            byte[] buffer = Encoding.ASCII.GetBytes(jsonMessage + "\n");
            stream.Write(buffer, 0, buffer.Length);
        }

        private void SendInfoMessage(TcpClient client, string content)
        {
            NetworkStream stream = client.GetStream();
            var message = new { type = "info", content = content };
            string jsonMessage = JsonConvert.SerializeObject(message);
            byte[] buffer = Encoding.ASCII.GetBytes(jsonMessage + "\n");
            stream.Write(buffer, 0, buffer.Length);
        }

        private bool CheckWin(char symbol)
        {
            for (int i = 0; i < 3; i++)
            {
                if ((board[i, 0] == symbol && board[i, 1] == symbol && board[i, 2] == symbol) ||
                    (board[0, i] == symbol && board[1, i] == symbol && board[2, i] == symbol))
                {
                    return true;
                }
            }

            if ((board[0, 0] == symbol && board[1, 1] == symbol && board[2, 2] == symbol) ||
                (board[0, 2] == symbol && board[1, 1] == symbol && board[2, 0] == symbol))
            {
                return true;
            }

            return false;
        }

        private bool CheckDraw()
        {
            foreach (var cell in board)
            {
                if (cell == '\0')
                {
                    return false;
                }
            }
            return true;
        }

        private void ResetGame()
        {
            Array.Clear(board, 0, board.Length);
            currentPlayer = player1;
            LogMessage("Game reset.");
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now}: {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void ServerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopServer();
        }
    }
}