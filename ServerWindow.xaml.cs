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
        private List<TcpClient> waitingClients = new List<TcpClient>();
        private readonly object lockObj = new object();

        public ServerWindow(string ipAddress, int port)
        {
            InitializeComponent();
            IPTextBlock.Text = ipAddress;
            PortTextBlock.Text = port.ToString();
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                StopServer();
            }
            else
            {
                StartServer();
            }
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
            StartStopButton.Content = "Stop Server";
            LogMessage("Server started...");
        }

        private void StopServer()
        {
            lock (lockObj)
            {
                isRunning = false;
                server.Stop();
            }
            if (serverThread != null && serverThread.IsAlive)
            {
                serverThread.Join();
            }
            StartStopButton.Content = "Start Server";
            LogMessage("Server stopped...");
        }

        // Główna pętla serwera
        private void ServerLoop()
        {
            try
            {
                while (true)
                {
                    lock (lockObj)
                    {
                        if (!isRunning)
                        {
                            break;
                        }
                    }

                    TcpClient client = server.AcceptTcpClient();
                    LogMessage("Client connected...");

                    lock (lockObj)
                    {
                        if (waitingClients.Count > 0)
                        {
                            TcpClient waitingClient = waitingClients[0];
                            waitingClients.RemoveAt(0);
                            Thread clientThread = new Thread(() => HandleClientPair(waitingClient, client)) { IsBackground = true };
                            clientThread.Start();
                        }
                        else
                        {
                            waitingClients.Add(client);
                            Thread clientThread = new Thread(() => HandleWaitingClient(client)) { IsBackground = true };
                            clientThread.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.Message}");
            }
        }

        // Obsługa oczekującego klienta
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

        // Obsługa pary klientów
        private void HandleClientPair(TcpClient client1, TcpClient client2)
        {
            try
            {
                NetworkStream stream1 = client1.GetStream();
                NetworkStream stream2 = client2.GetStream();

                var message = new { type = "info", content = "Paired with another player. Start playing!" };
                string jsonMessage = JsonConvert.SerializeObject(message);
                byte[] buffer1 = Encoding.ASCII.GetBytes(jsonMessage + "\n");
                stream1.Write(buffer1, 0, buffer1.Length);
                byte[] buffer2 = Encoding.ASCII.GetBytes(jsonMessage + "\n");
                stream2.Write(buffer2, 0, buffer2.Length);

                Thread thread1 = new Thread(() => RelayMessages(client1, client2)) { IsBackground = true };
                Thread thread2 = new Thread(() => RelayMessages(client2, client1)) { IsBackground = true };
                thread1.Start();
                thread2.Start();
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.Message}");
            }
        }

        private void RelayMessages(TcpClient fromClient, TcpClient toClient)
        {
            try
            {
                NetworkStream fromStream = fromClient.GetStream();
                NetworkStream toStream = toClient.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = fromStream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    toStream.Write(buffer, 0, bytesRead);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex.Message}");
            }
            finally
            {
                fromClient.Close();
                toClient.Close();
                LogMessage("Client disconnected...");
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
