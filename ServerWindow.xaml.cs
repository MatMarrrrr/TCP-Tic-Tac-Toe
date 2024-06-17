using System;
using System.Collections;
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
        private Queue<TcpClient> clientQueue = new Queue<TcpClient>();
        private List<GameSession> gameSessions = new List<GameSession>();
        private object lockObject = new object();
        private bool serverStarted = true;

        public ServerWindow(string ipAddress, int port)
        {
            InitializeComponent();
            IPTextBlock.Text = ipAddress;
            PortTextBlock.Text = port.ToString();
            StartServer();
        }

        private void StartServer()
        {
            try
            {
                string ipAddress = IPTextBlock.Text;
                int port = int.Parse(PortTextBlock.Text);

                server = new TcpListener(IPAddress.Parse(ipAddress), port);
                server.Start();

                serverThread = new Thread(ServerLoop) { IsBackground = true };
                serverThread.Start();

                LogMessage($"Server started at {ipAddress}:{port}");
            }
            catch (Exception ex)
            {
                LogMessage($"Exception while starting server: {ex.Message}");
            }
        }

        private void StopServer()
        {
            serverStarted = false;
            server.Stop();

            lock (lockObject)
            {
                foreach (var client in clientQueue)
                {
                    client.Close();
                }
                clientQueue.Clear();
                foreach (var session in gameSessions)
                {
                    session.Player1?.Close();
                    session.Player2?.Close();
                }
                gameSessions.Clear();
            }

            if (serverThread != null && serverThread.IsAlive)
            {
                serverThread.Join();
            }
        }

        private void ServerLoop()
        {
            try
            {
                while (serverStarted)
                {
                    if (server.Pending())
                    {
                        TcpClient client = server.AcceptTcpClient();
                        LogMessage($"Client connected: {client.Client.RemoteEndPoint}");

                        lock (lockObject)
                        {
                            clientQueue.Enqueue(client);
                            if (clientQueue.Count == 1)
                            {
                                Thread monitorThread = new Thread(() => MonitorClientConnection(client)) { IsBackground = true };
                                monitorThread.Start();
                                SendMessage(client, new { type = "info", value = "Waiting for another player..." });
                            }
                            else if (clientQueue.Count == 2)
                            {
                                TcpClient player1 = clientQueue.Dequeue();
                                TcpClient player2 = clientQueue.Dequeue();
                                StartGame(player1, player2);
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
                if (serverStarted)
                {
                    LogMessage($"SocketException: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.Message}");
            }
        }

        private void StartGame(TcpClient player1, TcpClient player2)
        {
            var session = new GameSession(player1, player2);

            lock (lockObject)
            {
                gameSessions.Add(session);
            }

            SendMessage(player1, new { type = "info", value = "Game has started" });
            SendMessage(player2, new { type = "info", value = "Game has started" });
            SendMessage(player1, new { type = "assign", symbol = "X", turn = true });
            SendMessage(player2, new { type = "assign", symbol = "O", turn = false });

            Thread player1Thread = new Thread(() => HandleClientCommunication(session, player1, player2)) { IsBackground = true };
            Thread player2Thread = new Thread(() => HandleClientCommunication(session, player2, player1)) { IsBackground = true };

            player1Thread.Start();
            player2Thread.Start();
        }

        private void MonitorClientConnection(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;

                while (serverStarted && (bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0) ;

                lock (lockObject)
                {
                    if (clientQueue.Contains(client))
                    {
                        Queue<TcpClient> newQueue = new Queue<TcpClient>(clientQueue.Where(c => c != client));
                        clientQueue = newQueue;
                        LogMessage($"Client disconnected: {client.Client.RemoteEndPoint}");
                    }
                }
            }
            catch
            {
                lock (lockObject)
                {
                    if (clientQueue.Contains(client))
                    {
                        Queue<TcpClient> newQueue = new Queue<TcpClient>(clientQueue.Where(c => c != client));
                        clientQueue = newQueue;
                        LogMessage($"Client disconnected: {client.Client.RemoteEndPoint}");
                    }
                }
            }

        }

        private void HandleClientCommunication(GameSession session, TcpClient client, TcpClient opponent)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;
                while (!session.IsFinished && serverStarted && (bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    var jsonMessage = JsonConvert.DeserializeObject<dynamic>(message);
                    string messageType = jsonMessage.type;
                    switch (messageType)
                    {
                        case "move":
                            int row = jsonMessage.row;
                            int column = jsonMessage.column;
                            char symbol = jsonMessage.symbol;
                            session.MakeMove(row, column, symbol);

                            if (session.CheckWin(symbol))
                            {
                                SendMessage(opponent, new { type = "move", row, column, symbol });
                                SendMessage(client, new { type = "finish", value = symbol + " has won" });
                                SendMessage(opponent, new { type = "finish", value = symbol + " has won" });
                            }
                            else if (session.CheckDraw())
                            {
                                SendMessage(opponent, new { type = "move", row, column, symbol });
                                SendMessage(client, new { type = "finish", value = $"It's a draw" });
                                SendMessage(opponent, new { type = "finish", value = $"It's a draw" });
                            }
                            else
                            {
                                SendMessage(opponent, new { type = "move", row, column, symbol });
                            }
                            break;
                        case "playAgain":
                            session.ResetGame();
                            SendMessage(client, new { type = "reset" });
                            SendMessage(opponent, new { type = "reset" });
                            SendMessage(opponent, new { type = "assign", symbol = "X", turn = true });
                            SendMessage(client, new { type = "assign", symbol = "O", turn = false });
                            break;
                    }

                    LogMessage($"Received message: {message}");
                }

                lock (lockObject)
                {
                    gameSessions.Remove(session);
                }
                SendMessage(opponent, new { type = "disconnected" });
                
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.Message}");
            }
        }

        private void SendMessage(TcpClient client, object message)
        {
            LogMessage($"Sent message: {message.ToString()}");
            NetworkStream stream = client.GetStream();
            string jsonMessage = JsonConvert.SerializeObject(message);
            byte[] buffer = Encoding.ASCII.GetBytes(jsonMessage + "\n");
            stream.Write(buffer, 0, buffer.Length);
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
