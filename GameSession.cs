using System;
using System.Net.Sockets;

namespace ClientServerGame
{
    public class GameSession
    {
        public TcpClient Player1 { get; private set; }
        public TcpClient Player2 { get; private set; }
        public char[,] Board { get; private set; }
        public bool IsFinished { get; set; }

        public GameSession(TcpClient player1, TcpClient player2)
        {
            Player1 = player1;
            Player2 = player2;
            Board = new char[3, 3];
            IsFinished = false;
        }

        public bool CheckWin(char symbol)
        {
            for (int i = 0; i < 3; i++)
            {
                if ((Board[i, 0] == symbol && Board[i, 1] == symbol && Board[i, 2] == symbol) ||
                    (Board[0, i] == symbol && Board[1, i] == symbol && Board[2, i] == symbol))
                {
                    return true;
                }
            }

            if ((Board[0, 0] == symbol && Board[1, 1] == symbol && Board[2, 2] == symbol) ||
                (Board[0, 2] == symbol && Board[1, 1] == symbol && Board[2, 0] == symbol))
            {
                return true;
            }

            return false;
        }

        public bool CheckDraw()
        {
            foreach (var cell in Board)
            {
                if (cell == '\0')
                {
                    return false;
                }
            }
            return true;
        }

        public void ResetGame()
        {
            Array.Clear(Board, 0, Board.Length);
            IsFinished = false;
        }

        public void MakeMove(int row, int col, char symbol)
        {
            Board[row, col] = symbol;
        }
    }
}
