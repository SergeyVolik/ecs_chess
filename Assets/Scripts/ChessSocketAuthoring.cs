using Unity.Entities;
using UnityEditor.SceneManagement;
using UnityEngine;


public class ChessSocketAuthoring : MonoBehaviour
{
    public GameObject highlightSelected;
    public GameObject highlightMovePos;
    public GameObject highlightEnemy;

    public class Baker : Baker<ChessSocketAuthoring>
    {
        public override void Bake(ChessSocketAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessSocketC>(entity, new ChessSocketC
            {

            });
            AddComponent<ChessSocketHighlightInstanceC>(entity);
            AddComponent<ChessSocketHighlightC>(entity, new ChessSocketHighlightC
            {
                highlightEnemyPrefab = GetEntity(authoring.highlightEnemy, TransformUsageFlags.Dynamic),
                highlightSelectedPrefab = GetEntity(authoring.highlightSelected, TransformUsageFlags.Dynamic),
                highlightMovePosPrefab = GetEntity(authoring.highlightMovePos, TransformUsageFlags.Dynamic),
            });
        }
    }
}