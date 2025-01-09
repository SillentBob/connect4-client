using System;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TicTacToeClient : MonoBehaviour
{
    
}

public enum MessageId : byte
{
    PlayerId = 1,
    ActivePlayerId = 2,
    Move = 3,
    BoardUpdate = 4,
    Winner = 5
}