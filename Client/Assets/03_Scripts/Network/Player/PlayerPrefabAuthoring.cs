using Unity.Entities;
using UnityEngine;

public class PlayerPrefabAuthoring : MonoBehaviour
{
    public GameObject prefab;

    class Baker : Baker<PlayerPrefabAuthoring>
    {
        public override void Bake(PlayerPrefabAuthoring authoring)
        {
            var prefabEntity = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic);
            var thisEntity = GetEntity(TransformUsageFlags.None); // 이 컴포넌트가 붙은 오브젝트
            AddComponent(thisEntity, new PlayerPrefab { Value = prefabEntity });
        }
    }
}
public struct PlayerPrefab : IComponentData
{
    public Entity Value;
}
