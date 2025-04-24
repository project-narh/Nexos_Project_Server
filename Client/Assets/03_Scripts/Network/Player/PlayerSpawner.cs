using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


public struct PlayerInfo : IComponentData
{
    public int PlayerId;
    public int Uid;
}

public static class PlayerSpawner
{
    public static Entity SpawnPlayer(
        EntityManager em,
        Entity prefab,
        int playerId,
        int uid,
        float3 pos,
        quaternion rot,
        bool isSelf)
    {
        if (prefab == Entity.Null)
            return Entity.Null;

        Entity entity = em.Instantiate(prefab);

        em.SetComponentData(entity, new LocalTransform
        {
            Position = pos,
            Rotation = rot,
            Scale = 1f
        });

        em.AddComponentData(entity, new PlayerInfo
        {
            PlayerId = playerId,
            Uid = uid
        });

        if (isSelf)
        {
            em.AddComponent<IsMainPlayerTag>(entity);
        }
        else
        {
            em.AddComponent<IsOtherPlayerTag>(entity);
            em.AddComponentData(entity, new TargetTransform
            {
                Position = pos,
                Rotation = rot
            });
        }

        Debug.Log($"[SpawnPlayer] Instantiating: {prefab} / isSelf: {isSelf}"); 
        return entity;
    }

    public static void DespawnPlayer(EntityManager em, int uid)
    {
        EntityQuery query = em.CreateEntityQuery(typeof(PlayerInfo));
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        using var infos = query.ToComponentDataArray<PlayerInfo>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < infos.Length; i++)
        {
            if (infos[i].Uid == uid)
            {
                em.DestroyEntity(entities[i]);
                break;
            }
        }
    }
}
