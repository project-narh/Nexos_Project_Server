using System;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;

public struct Boundary : IComponentData
{
    public int      sectionFloor;
    public float4   sectionBoundaries;
}

public class BoundaryAuthoring : MonoBehaviour
{
    // 모든 바운더리는 층 값을 가지며 플레이어가 있는 층에 따라 스트리밍됨
    // 층이 -1일 경우 층 체크를 무시
    // sectionBoundaries는 각각 position 상대 -x, -z, +x, +z 값을 의미함
    // bake 시점에 상대값에서 절대값으로 변경됨
    public int      _sectionFloor = 1;
    public float4   _sectionBoundaries = new float4(-5, -5, 5, 5);

    private class Baker : Baker<BoundaryAuthoring>
    {
        public override void Bake(BoundaryAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var pos = GetComponent<Transform>().position;
            
            AddComponent(entity, new Boundary
            {
                sectionFloor    = authoring._sectionFloor,
                
                // sectionBorders 는 Bake 시점에서 해당 GameObject의 좌표 대비
                // 상대 좌표에서 Scene 내 절대 좌표로 변환됨
                // - System 에서 계산을 줄임
                sectionBoundaries = new float4(
                    pos.x + authoring._sectionBoundaries.x,
                    pos.z + authoring._sectionBoundaries.y,
                    pos.x + authoring._sectionBoundaries.z,
                    pos.z + authoring._sectionBoundaries.w)
            });
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 매우 비효율적이니 나중에 고칠 것
        Color color = Color.yellow;

        float3[] mat = new float3[4];
        for (int i = 0; i < 4; i++) mat[i] = transform.position;
        
        // 으윽
        mat[0] += new float3(_sectionBoundaries.x, 0f, _sectionBoundaries.y);
        mat[1] += new float3(_sectionBoundaries.x, 0f, _sectionBoundaries.w);
        mat[2] += new float3(_sectionBoundaries.z, 0f, _sectionBoundaries.y);
        mat[3] += new float3(_sectionBoundaries.z, 0f, _sectionBoundaries.w);

        Debug.DrawLine(mat[0], mat[1], color);
        Debug.DrawLine(mat[0], mat[2], color);
        Debug.DrawLine(mat[2], mat[3], color);
        Debug.DrawLine(mat[3], mat[1], color);
    }
}

// SceneSection 설정을 위한 커스텀 베이킹 시스템
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
partial struct BoundaryBakingSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var cleanupQuery        = SystemAPI.QueryBuilder().WithAll<Boundary, SectionMetadataSetup>().Build();
        state.EntityManager.RemoveComponent<Boundary>(cleanupQuery);

        // 엔티티와 컴포넌트 데이터 배열을 만들기 위한 작업
        var boundaryQuery       = SystemAPI.QueryBuilder().WithAll<Boundary, SceneSection>().Build();
        var boundaries          = boundaryQuery.ToComponentDataArray<Boundary>(Allocator.Temp);
        var boundaryEntities    = boundaryQuery.ToEntityArray(Allocator.Temp);

        var sectionQuery        = SystemAPI.QueryBuilder().WithAll<SectionMetadataSetup>().Build();

        // SceneSection 베이킹 완료
        for (int index = 0; index < boundaryEntities.Length; ++index)
        {
            var sceneSection    = state.EntityManager.GetSharedComponent<SceneSection>(boundaryEntities[index]);
            var sectionEntity   = SerializeUtility.GetSceneSectionEntity(sceneSection.Section, state.EntityManager,
                ref sectionQuery, true);
            state.EntityManager.AddComponentData(sectionEntity, boundaries[index]);
        }
    }
}