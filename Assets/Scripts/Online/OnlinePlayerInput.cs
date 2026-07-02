using Fusion;
using UnityEngine;

public struct OnlinePlayerInput : INetworkInput
{
    public Vector2 Move;
    public bool PlaceBomb;
}