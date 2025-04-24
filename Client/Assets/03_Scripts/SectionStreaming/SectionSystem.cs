using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;

partial struct SectionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Boundary>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        NativeHashSet<Entity> toLoad = new NativeHashSet<Entity>(1, Allocator.Temp);

        // 쿼리빌더로 쿼리를 만든 다음 이렇게 엔티티, 컴포넌트데이터 배열들을 만들면
        // 평소와 비슷한 느낌으로 반복문을 만들 수 있음
        var sectionQuery    = SystemAPI.QueryBuilder().WithAll<Boundary, SceneSectionData>().Build();
        var sectionEntities = sectionQuery.ToEntityArray(Allocator.Temp);
        var boundaryArray   = sectionQuery.ToComponentDataArray<Boundary>(Allocator.Temp);

        // 샘플과는 달리 localTransform 외에도 층 값이 필요하므로 relevant 액세스가 필요함
        foreach (var (transform, relevant) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Relevant>>())
        {
            for (int index = 0; index < boundaryArray.Length; ++index)
            {
                Color debugColor = new Color(1f, 0f, 0f);
                
                // 플레이어와 같은 층의 섹션인지 우선 확인하고, 섹션의 네모 범위 내에 존재하는지 확인
                // sectionFloor 가 -1일 경우에는 층 조건을 무시하고 늘 플레이어와 같은 층인 것으로 간주
                if (boundaryArray[index].sectionFloor == -1 || boundaryArray[index].sectionFloor == relevant.ValueRO.currentFloor)
                {
                    if (CheckInsideSquare(transform.ValueRO.Position, boundaryArray[index].sectionBoundaries))
                    {
                        toLoad.Add(sectionEntities[index]);
                        debugColor = new Color(0f, 0.5f, 0f);
                    }
                }
                
                // 네모난 범위 기즈모 출력
                DrawBoundariesXZ(boundaryArray[index].sectionBoundaries, debugColor);
            }
        }

        // 로드 요청에 따라 섹션 로드/언로드
        foreach (Entity sectionEntity in sectionEntities)
        {
            var sectionState = SceneSystem.GetSectionStreamingState(state.WorldUnmanaged, sectionEntity);
            if (toLoad.Contains(sectionEntity))
            {
                if (sectionState == SceneSystem.SectionStreamingState.Unloaded)
                {
                    state.EntityManager.AddComponent<RequestSceneLoaded>(sectionEntity);
                }
            }
            else
            {
                if (sectionState != SceneSystem.SectionStreamingState.Unloaded)
                {
                    state.EntityManager.RemoveComponent<RequestSceneLoaded>(sectionEntity);
                }
            }
        }
    }

    // 간단한 네모 범위 확인 함수
    public static bool CheckInsideSquare(float3 positionToCheck, float4 boundaries)
    {
        if (positionToCheck.x >= boundaries.x &&
            positionToCheck.z >= boundaries.y &&
            positionToCheck.x <= boundaries.z &&
            positionToCheck.z <= boundaries.w) return true;
        else return false;
    }

    // 네모 범위 기즈모
    public static void DrawBoundariesXZ(float4 boundaries, Color color)
    {
        float3 aa, ab, ac, ad;
        aa = new float3(boundaries.x, 0f, boundaries.y);
        ab = new float3(boundaries.x, 0f, boundaries.w);
        ac = new float3(boundaries.z, 0f, boundaries.y);
        ad = new float3(boundaries.z, 0f, boundaries.w);

        Debug.DrawLine(aa, ab, color);
        Debug.DrawLine(aa, ac, color);
        Debug.DrawLine(ac, ad, color);
        Debug.DrawLine(ad, ab, color);
    }
}