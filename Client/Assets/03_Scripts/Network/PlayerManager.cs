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
    public static PlayerManager Instance;
    bool isSpawn = false;
    private Dictionary<int, Entity> playerEntityMap = new();

    public Entity playerPrefabEntity;
    public Entity mainPlayerEntity { get; private set; }

    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            StartCoroutine(Start_ECS());
        }
        else
        {
            Debug.LogError("[PlayerManager] PlayerManager 인스턴스가 중복 생성되었습니다.");
            Destroy(gameObject);
            return;
        }
        
    }

    IEnumerator Start_ECS()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(PlayerPrefab));
        //PlayerPrefab가 붙은 엔티티 생길때 까지 대기
        while (query.CalculateEntityCount() != 1)
            yield return null;

        var entity = query.GetSingletonEntity(); // ← 여기서 GetSingletonEntity 가능
        playerPrefabEntity = em.GetComponentData<PlayerPrefab>(entity).Value;
        Debug.Log($"[ECS Init] Loaded prefab entity: {playerPrefabEntity}");
        isSpawn = true;
        Debug.Log("[PlayerManager] PlayerPrefab 엔티티 로드 완료.");
    }

    public void Player_Spawn(S_PlayerList listPacket)
    {
        StartCoroutine(Start_SpawnList(listPacket));
    }

    IEnumerator Start_SpawnList(S_PlayerList listPacket)
    {
        Debug.Log($"[Manager] 소환 시작");
        while (!isSpawn)
            yield return null;

        Debug.Log($"[Manager] Player Spawn List {listPacket.players.Count}");
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        foreach (S_PlayerList.Player p in listPacket.players)
        {
            Debug.Log($"[PlayerManager] 리스트 소환. {p.playerId}   {p.position}   {p.rotation}");
            float3 pos = new float3(p.position.x, p.position.y, p.position.z);
            quaternion rot = new quaternion(p.rotation.x, p.rotation.y, p.rotation.z, p.rotation.w);

            var spawned = PlayerSpawner.SpawnPlayer(em, playerPrefabEntity, p.playerId, p.uid, pos, rot, p.isSelf);
            if (spawned == Entity.Null)
                Debug.LogError("[PlayerManager] Entity.Null 반환됨 (소환 실패)");
            playerEntityMap[p.playerId] = spawned;
            if (p.isSelf && spawned != Entity.Null)
            {
                mainPlayerEntity = spawned;
                Debug.Log($"[PlayerManager] 플레이어");
                StartCoroutine(NextFrame(spawned));
            }
        }
    }

    private IEnumerator NextFrame(Entity Entity)
    {
        int waitFrame = 2;
        while (waitFrame-- > 0)
            yield return null;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(FirstPersonPlayer));

        if (query.CalculateEntityCount() == 1)
        {
            var controllerEntity = query.GetSingletonEntity();
            var controller = em.GetComponentData<FirstPersonPlayer>(controllerEntity);
            controller.ControlledCharacter = Entity;
            em.SetComponentData(controllerEntity, controller);

            Debug.Log($"[PlayerManager] ControlledCharacter 연결 완료: {Entity}");
            if (controller.ControlledCharacter != mainPlayerEntity)
                Debug.LogError("ControlledCharacter가 본인이 아님!");
        }
    }
    public void PlayerEnter(S_BroadcastEnterGame packet)
    {
        Debug.Log("[Manager] Player Enter");
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (mainPlayerEntity != Entity.Null)
        {
            if (packet.playerId == em.GetComponentData<PlayerInfo>(mainPlayerEntity).PlayerId)
                return;
        }

        float3 pos = new float3(packet.position.x, packet.position.y, packet.position.z);
        quaternion rot = new quaternion(packet.rotation.x, packet.rotation.y, packet.rotation.z, packet.rotation.w);

        var entity = PlayerSpawner.SpawnPlayer(em, playerPrefabEntity, packet.playerId, packet.uid, pos, rot, false);
        playerEntityMap[packet.playerId] = entity;
        // 여기서도 추가적인 확인이 필요하면 진행
        if (entity != Entity.Null)
        {
            Debug.Log($"[PlayerManager] 다른 플레이어 등장: {packet.playerId}");
        }
    }
    public void Move(S_BroadcastMove packet)
    {
        Debug.Log($"[PlayerManager] 이동: {packet.playerId}  위치 : {packet.position}  회전 : {packet.rotation}");
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (playerEntityMap.TryGetValue(packet.playerId, out Entity entity))
        {
            if (!em.HasComponent<TargetTransform>(entity)) return;
            if (em.HasComponent<IsMainPlayerTag>(entity)) return;

            em.SetComponentData(entity, new TargetTransform
            {
                Position = packet.position,
                Rotation = packet.rotation
            });
        }
    }
}

