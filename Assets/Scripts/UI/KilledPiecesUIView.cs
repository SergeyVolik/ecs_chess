using System.Collections.Generic;
using UnityEngine;

public class KilledPiecesUIView : MonoBehaviour
{
    public bool whiteView;

    public GameObject pawPrefab;
    public GameObject rookPrefab;
    public GameObject bishopPrefab;
    public GameObject knightPrefab;
    public GameObject queenPrefab;

    private List<GameObject> m_items = new List<GameObject>();
    internal void AddPiece(ChessType chessType)
    {
        GameObject prefab = null;
        switch (chessType)
        {
            case ChessType.Pawn:
                prefab = pawPrefab;
                break;
            case ChessType.Bishop:
                prefab = bishopPrefab;

                break;
            case ChessType.Rook:
                prefab = rookPrefab;

                break;
            case ChessType.Knight:
                prefab = knightPrefab;

                break;
            case ChessType.Queen:
                prefab = queenPrefab;

                break;
            case ChessType.King:

                break;
            default:
                break;
        }
        var instance = GameObject.Instantiate(prefab, transform);
        m_items.Add(instance);
    }

    internal void Clear()
    {
        foreach (var item in m_items)
        {
            GameObject.Destroy(item);
        }

        m_items.Clear();
    }
}
