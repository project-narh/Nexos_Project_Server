using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using static S_PlayerList;

public class PlayerManager : MonoBehaviour
{
    public Entity playerPrefabEntity;
    public Entity mainPlayerEntity { get; private set; }

    void Awake()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (em.HasComponent<PlayerPrefab>(em.CreateEntityQuery(typeof(PlayerPrefab)).GetSingletonEntity()))
        {
            playerPrefabEntity = em.CreateEntityQuery(typeof(PlayerPrefab)).GetSingleton<PlayerPrefab>().Value;
        }
        else
        {
            Debug.LogError("[PlayerManager] PlayerPrefab 컴포넌트를 찾을 수 없습니다.");
        }
    }

    public void Player_Spawn(S_PlayerList listPacket)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        foreach (S_PlayerList.Player p in listPacket.players)
        {
            float3 pos = new float3(p.position.x, p.position.y, p.position.z);
            quaternion rot = new quaternion(p.rotation.x, p.rotation.y, p.rotation.z, p.rotation.w);

            var spawned = PlayerSpawner.SpawnPlayer(em, playerPrefabEntity, p.playerId, p.uid, pos, rot, p.isSelf);

            if (p.isSelf && spawned != Entity.Null)
            {
                mainPlayerEntity = spawned;
            }
        }
    }

    public void PlayerEnter(S_BroadcastEnterGame packet)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (mainPlayerEntity != Entity.Null)
        {
            if (packet.playerId == em.GetComponentData<PlayerInfo>(mainPlayerEntity).PlayerId)
                return;
        }

        float3 pos = new float3(packet.position.x, packet.position.y, packet.position.z);
        quaternion rot = new quaternion(packet.rotation.x, packet.rotation.y, packet.rotation.z, packet.rotation.w);

        PlayerSpawner.SpawnPlayer(em, playerPrefabEntity, packet.playerId, packet.uid, pos, rot, false);
    }
}

