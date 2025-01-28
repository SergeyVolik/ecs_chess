using UnityEngine;

[CreateAssetMenu]
public class ChessBoardConfigurationSO : ScriptableObject
{
    public SpawnPieceData[] white;

    public SpawnPieceData[] black;

    public bool currentTurnWhite;
}