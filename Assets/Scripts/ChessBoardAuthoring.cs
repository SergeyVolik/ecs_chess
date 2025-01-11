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

public class ChessBoardAuthoring : MonoBehaviour
{
    public Vector3 spawnGridOffset;
    public Vector3 offsetBetweenSockets;
    public GameObject socketPrefab;

    public ChessPieces black;
    public ChessPieces white;

    public class Baker : Baker<ChessBoardAuthoring>
    {
        public override void Bake(ChessBoardAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessBoardC>(entity, new ChessBoardC
            {
                offsetBetweenSockets = authoring.offsetBetweenSockets,
                spawnGridOffset = authoring.spawnGridOffset,
                socketPrefab = GetEntity(authoring.socketPrefab, TransformUsageFlags.Dynamic),
                black = GetPrefabs(authoring.black),
                white = GetPrefabs(authoring.white)
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