using Unity.Entities;
using UnityEngine;

public class RelevantAuthoring : MonoBehaviour
{
    // 플레이어의 현재 층을 의미함. 런타임중 층 값이 다른 범위 섹션들은 언로드됨
    public int _currentFloor = 1;
    
    private class Baker : Baker<RelevantAuthoring>
    {
        public override void Bake(RelevantAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Relevant
            {
                currentFloor = authoring._currentFloor
            });
        }
    }
}

public struct Relevant : IComponentData
{
    public int currentFloor;
}
