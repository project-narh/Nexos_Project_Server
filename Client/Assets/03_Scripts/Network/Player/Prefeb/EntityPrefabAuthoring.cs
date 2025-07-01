using UnityEngine;
using Unity.Entities;

public struct PlayerInfo : IComponentData
{
    public ulong PlayerID;
}

public class EntityPrefabAuthoring : MonoBehaviour
{
    public GameObject prefab;

    class Baker : Baker<EntityPrefabAuthoring>
    {
        public override void Bake(EntityPrefabAuthoring authoring)
        {
            Debug.Log("[EntityPrefabAuthoring] Baker 시작!");
            Debug.Log($"authoring.prefab: {authoring.prefab?.name ?? "NULL"}");

            if (authoring.prefab == null)
            {
                Debug.LogError("authoring.prefab이 null입니다!");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);
            Debug.Log($"Holder Entity: {entity}");

            try
            {
                var prefabEntity = GetEntity(authoring.prefab, TransformUsageFlags.Renderable);
                Debug.Log($"Prefab Entity: {prefabEntity}");

                if (prefabEntity == Entity.Null)
                {
                    Debug.LogError("GetEntity가 Entity.Null을 반환했습니다!");
                    return;
                }

                AddComponent(entity, new EntityPrefab
                {
                    Prefab = prefabEntity
                });

                Debug.Log("EntityPrefab 컴포넌트 추가 성공!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Baker에서 예외 발생: {e.Message}");
                Debug.LogError($"Stack: {e.StackTrace}");
            }
        }
    }
}

public struct EntityPrefab : IComponentData
{
    public Entity Prefab;
}

public struct OwnerOnlyTag : IComponentData { }
