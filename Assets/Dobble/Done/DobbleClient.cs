using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace DobbleDone
{
    public class DobbleClient : MonoBehaviour
    {
        #region Types

        public enum MessageId : byte
        {
            PlayerId   = 1,
            NewCenter  = 2,
            NewYours   = 3,
            PlayerMove = 4,
            Banned     = 5,
            Score      = 6
        }

        #endregion Types

        #region Variables

        [SerializeField] private Button[]        _Buttons;
        [SerializeField] private Image[]         _CenterImages;
        [SerializeField] private TextMeshProUGUI _StateText;
        [SerializeField] private GameObject      _BannedText;
        [SerializeField] private Sprite[]        _Symbols;

        private Socket _Server;
        private int    _MyId;
        private float  _BannedTimer;
        private bool   _GameStarted;

        private readonly byte[]       _PlayerSymbolsIds = new byte[8];
        private readonly byte[]       _ReceiveBuffer    = new byte[_MaxMessageSize];
        private const    int          _MaxMessageSize   = 1024;

        #endregion Variables

        #region Special Methods

        private void Awake()
        {
            _MyId = -1;
            ConnectButtons();
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

        private void Update()
        {
            if (_BannedTimer <= 0) return;
            _BannedTimer -= Time.deltaTime;
            if (_BannedTimer > 0) return;
            _BannedText.gameObject.SetActive(false);
        }

        #endregion Special Methods

        #region Private Methods

        private void ConnectButtons()
        {
            for (int i = 0; i < _Buttons.Length; ++i)
            {
                int x = i;
                _Buttons[i].onClick.AddListener(() => ButtonClicked(x));
            }
        }

        private void ButtonClicked(int x)
        {
            if (!_GameStarted) return;
            if (_BannedTimer > 0) return;

            byte[] move = { (byte)MessageId.PlayerMove, _PlayerSymbolsIds[x] };
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
                    case (byte)MessageId.NewYours:
                    {
                        List<int> buttonIndexes = Enumerable.Range(0, 8).ToList();

                        for (int i = 0; i < _PlayerSymbolsIds.Length; ++i)
                        {
                            int randomIndex = Random.Range(0, buttonIndexes.Count);
                            int index       = buttonIndexes[randomIndex];
                            buttonIndexes.RemoveAt(randomIndex);

                            _PlayerSymbolsIds[index]     = message[currentIndex++];
                            _Buttons[index].image.sprite = _Symbols[_PlayerSymbolsIds[index]];
                        }

                        break;
                    }
                    case (byte)MessageId.NewCenter:
                    {
                        _BannedTimer = 0;
                        _BannedText.gameObject.SetActive(false);

                        if (!_GameStarted)
                        {
                            _GameStarted    = true;
                            _StateText.text = "You 0/10 vs Opponent 0/10";
                        }

                        for (int i = 0; i < _PlayerSymbolsIds.Length; ++i)
                        {
                            _CenterImages[i].sprite = _Symbols[message[currentIndex++]];
                        }

                        break;
                    }
                    case (byte)MessageId.Banned:
                    {
                        _BannedTimer = 5;
                        _BannedText.gameObject.SetActive(true);
                        Debug.Log("Banned");
                        break;
                    }
                    case (byte)MessageId.Score:
                    {
                        byte playerOneScore = message[currentIndex++];
                        byte playerTwoScore = message[currentIndex++];

                        if (playerOneScore == 10 || playerTwoScore == 10)
                        {
                            bool won = playerOneScore == 10 && _MyId == 0 || playerTwoScore == 10 && _MyId == 1;
                            _StateText.text = won ? "Game over. Victory." : "Game over. Defeat.";
                        }
                        else
                        {
                            byte myScore       = _MyId == 0 ? playerOneScore : playerTwoScore;
                            byte opponentScore = _MyId == 0 ? playerTwoScore : playerOneScore;
                            _StateText.text = $"You {myScore}/10 vs Opponent {opponentScore}/10";
                        }

                        break;
                    }
                }
            }
        }

        #endregion Private Methods
    }
}