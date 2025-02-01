using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class ChessPieces
{
    public GameObject bishop;
    public GameObject king;
    public GameObject knight;
    public GameObject pawn;
    public GameObject queen;
    public GameObject rook;
}

public class ChessBoardPersistentDataAuthoring : MonoBehaviour
{
    public Vector3 spawnGridOffset;
    public Vector3 offsetBetweenSockets;
    public GameObject socketPrefab;
    public GameObject chessBoard;

    public ChessPieces black;
    public ChessPieces white;

    public ChessPieces blackData;
    public ChessPieces whiteData;

    public class Baker : Baker<ChessBoardPersistentDataAuthoring>
    {
        public override void Bake(ChessBoardPersistentDataAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessBoardPersistentC>(entity, new ChessBoardPersistentC
            {
                offsetBetweenSockets = authoring.offsetBetweenSockets,
                spawnGridOffset = authoring.spawnGridOffset,
                socketPrefab = GetEntity(authoring.socketPrefab, TransformUsageFlags.Dynamic),
                chessBoardPrefab = GetEntity(authoring.chessBoard, TransformUsageFlags.Dynamic),
                blackPiecesMeshPrefabs = GetPrefabs(authoring.black),
                whitePiecesMeshPrefabs = GetPrefabs(authoring.white),
                blackPiecesDataPrefabs = GetPrefabs(authoring.blackData),
                whitePiecesDataPrefabs = GetPrefabs(authoring.whiteData),
            });
        }

        private ChessPiecesPrefabs GetPrefabs(ChessPieces pieces)
        {
            return new ChessPiecesPrefabs
            {
                bishop = GetEntity(pieces.bishop, TransformUsageFlags.Dynamic),
                king = GetEntity(pieces.king, TransformUsageFlags.Dynamic),
                knight = GetEntity(pieces.knight, TransformUsageFlags.Dynamic),
                pawn = GetEntity(pieces.pawn, TransformUsageFlags.Dynamic),
                queen = GetEntity(pieces.queen, TransformUsageFlags.Dynamic),
                rook = GetEntity(pieces.rook, TransformUsageFlags.Dynamic)
            };
        }
    }
}