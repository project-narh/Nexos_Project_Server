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
        
        var sectionQuery    = SystemAPI.QueryBuilder().WithAll<Boundary, SceneSectionData>().Build();
        var sectionEntities = sectionQuery.ToEntityArray(Allocator.Temp);
        var boundaryArray   = sectionQuery.ToComponentDataArray<Boundary>(Allocator.Temp);

        foreach (var (transform, relevant) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Relevant>>())
        {
            for (int index = 0; index < boundaryArray.Length; ++index)
            {
                Color debugColor = new Color(1f, 0f, 0f);
                
                if (boundaryArray[index].sectionFloor == -1 || boundaryArray[index].sectionFloor == relevant.ValueRO.currentFloor)
                {
                    if (CheckInsideSquare(transform.ValueRO.Position, boundaryArray[index].sectionBoundaries))
                    {
                        toLoad.Add(sectionEntities[index]);
                        debugColor = new Color(0f, 0.5f, 0f);
                    }
                }
                
                DrawBoundariesXZ(boundaryArray[index].sectionBoundaries, debugColor);
            }
        }

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

    public static bool CheckInsideSquare(float3 positionToCheck, float4 boundaries)
    {
        if (positionToCheck.x >= boundaries.x &&
            positionToCheck.z >= boundaries.y &&
            positionToCheck.x <= boundaries.z &&
            positionToCheck.z <= boundaries.w) return true;
        else return false;
    }
    
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