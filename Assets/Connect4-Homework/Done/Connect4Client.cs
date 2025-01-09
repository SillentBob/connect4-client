using System;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Connect4Done
{
    public class Connect4Client : MonoBehaviour
    {
        #region Types

        public enum MessageId : byte
        {
            PlayerId       = 1,
            ActivePlayerId = 2,
            Move           = 3,
            BoardUpdate    = 4,
            Winner         = 5
        }

        #endregion Types

        #region Variables

        [SerializeField] private GameObject      _Button;
        [SerializeField] private GameObject      _Image;
        [SerializeField] private RectTransform   _ButtonsParent;
        [SerializeField] private RectTransform   _ImagesParent;
        [SerializeField] private TextMeshProUGUI _StateText;
        [SerializeField] private Sprite[]        _PlayersSymbols;

        private Socket _Server;
        private int    _MyId;
        private int    _ActivePlayer;

        private readonly int[][]   _Board          = new int[6][];
        private readonly Image[][] _BoardImages    = new Image[6][];
        private readonly byte[]    _ReceiveBuffer  = new byte[_MaxMessageSize];
        private const    int       _MaxMessageSize = 1024;

        #endregion Variables

        #region Special Methods

        private void Awake()
        {
            _MyId         = -1;
            _ActivePlayer = -1;

            // Create board
            for (int i = 0; i < _Board.Length; ++i)
            {
                _Board[i]       = new[] { -1, -1, -1, -1, -1, -1, -1 };
                _BoardImages[i] = new Image[7];
            }

            SpawnButtons();
            SpawnImages();
        }

        private void Start()
        {
            IPEndPoint serverEp = new IPEndPoint(IPAddress.Loopback, 2222);
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = -1
            };

            try
            {
                server.Connect(serverEp);
            }
            catch (Exception)
            {
                Debug.LogError("Establish connection with server (" + serverEp + ") failed!");
                return;
            }

            _Server = server;
            _Server.BeginReceive(_ReceiveBuffer, 0, _MaxMessageSize, SocketFlags.None, ReceiveMessage, null);
        }

        #endregion Special Methods

        #region Private Methods

        private void SpawnButtons()
        {
            // Filling buttons from left to right
            float yMin = 0f;
            float yMax = 1f;

            for (int x = 0; x < 7; ++x)
            {
                float xMin = (1f / 7f) * x;
                float xMax = (1f / 7f) * (x + 1f);

                RectTransform newButton = Instantiate(_Button, Vector3.zero, Quaternion.identity).GetComponent<RectTransform>();
                newButton.SetParent(_ButtonsParent, false);
                newButton.anchorMin = new Vector2(xMin, yMin);
                newButton.anchorMax = new Vector2(xMax, yMax);
                int x1 = x;
                newButton.GetComponent<Button>().onClick.AddListener(() => ButtonClicked(x1));
            }
        }

        private void SpawnImages()
        {
            // Filling images from top to down, from left to right
            for (int y = 0; y < 6; ++y)
            {
                float yMin = (1f / 6f) * (5f - y);
                float yMax = (1f / 6f) * (6f - y);

                for (int x = 0; x < 7; ++x)
                {
                    float xMin = (1f / 7f) * x;
                    float xMax = (1f / 7f) * (x + 1f);

                    RectTransform newImage = Instantiate(_Image, Vector3.zero, Quaternion.identity).GetComponent<RectTransform>();
                    newImage.SetParent(_ImagesParent, false);
                    newImage.anchorMin = new Vector2(xMin, yMin);
                    newImage.anchorMax = new Vector2(xMax, yMax);
                    _BoardImages[y][x] = newImage.GetComponent<Image>();
                }
            }
        }

        private void ButtonClicked(int x)
        {
            if (_MyId != _ActivePlayer)
            {
                Debug.Log("This is not your turn.");
                return;
            }

            if (_Board[0][x] != -1)
            {
                Debug.Log("Can't put symbol there. It's not free.");
                return;
            }

            byte[] move = { (byte)MessageId.Move, (byte)x };
            _Server.Send(move);
        }

        private void ReceiveMessage(IAsyncResult ar)
        {
            int received = _Server.EndReceive(ar);

            if (received <= 0) return;
            byte[] recData = new byte[received];
            Buffer.BlockCopy(_ReceiveBuffer, 0, recData, 0, received);

            // Inside received data it can be more than one message
            // Because this will be executed on other thread we must enqueue this to be executed on unity main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessBytesFromMessage(recData));

            _Server.BeginReceive(_ReceiveBuffer, 0, _ReceiveBuffer.Length, SocketFlags.None, ReceiveMessage, null);
        }

        private void ProcessBytesFromMessage(byte[] message)
        {
            int currentIndex = 0;

            while (currentIndex < message.Length)
            {
                switch (message[currentIndex++])
                {
                    case (byte)MessageId.PlayerId:
                    {
                        _MyId = message[currentIndex++];
                        string symbol = _MyId == 0 ? "O" : "X";
                        Debug.Log($"My player id is {_MyId} ({symbol})");
                        break;
                    }
                    case (byte)MessageId.ActivePlayerId:
                    {
                        _ActivePlayer = message[currentIndex++];
                        string symbol = _ActivePlayer == 0 ? "O" : "X";
                        Debug.Log($"The active player is {_ActivePlayer} ({symbol})");
                        _StateText.text = _MyId == _ActivePlayer ? "It's your turn" : "Waiting for opponent move";
                        break;
                    }
                    case (byte)MessageId.BoardUpdate:
                    {
                        int x        = message[currentIndex++];
                        int y        = message[currentIndex++];
                        int playerId = message[currentIndex++];
                        Debug.Log($"Player {playerId} put symbol at {x} {y}");
                        _Board[y][x]              = playerId;
                        _BoardImages[y][x].sprite = _PlayersSymbols[playerId];
                        break;
                    }
                    case (byte)MessageId.Winner:
                    {
                        int winnerId = message[currentIndex++];
                        Debug.Log($"Game over. Winner id is {winnerId}");

                        if (winnerId == 2)
                        {
                            _StateText.text = "Game over. Draw.";
                        }
                        else if (winnerId == _MyId)
                        {
                            _StateText.text = "Game over. Victory.";
                        }
                        else
                        {
                            _StateText.text = "Game over. Defeat.";
                        }

                        break;
                    }
                }
            }
        }

        #endregion Private Methods
    }
}