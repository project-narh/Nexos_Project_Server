using Unity.Entities;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    private Entity playerEntity;
    private EntityManager entityManager;
    private ulong playerId;

    private void Awake()
    {
        enabled = false;
    }

    public void SetEntity(Entity entity, ulong playerId)
    {
        playerEntity = entity;
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        enabled = true;
        this.playerId = playerId;
    }

    public void ECSSpawn(ulong playerId, Vector3 pos, Quaternion rot)
    {
        Debug.Log($"[PlayerNetwork] InitPlayerClientRpc: playerId={playerId}");
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        Debug.Log($"[SpawnPlayer] World 존재: {World.DefaultGameObjectInjectionWorld != null}");
        Debug.Log($"[SpawnPlayer] EntityManager 존재: {em != null}");
        Debug.Log("[PlayerNetwork] 내 플레이어로 설정");

        var query = em.CreateEntityQuery(typeof(EntityPrefab));
        int count = query.CalculateEntityCount();
        Debug.Log($"[SpawnPlayer] EntityPrefab 개수: {count}");

        if (count > 0)
        {
            var prefabComponent = query.GetSingleton<EntityPrefab>();
            Debug.Log($"[SpawnPlayer] EntityPrefab.Prefab: {prefabComponent.Prefab}");
            Debug.Log($"[SpawnPlayer] Prefab 존재 여부: {em.Exists(prefabComponent.Prefab)}");

            if (em.Exists(prefabComponent.Prefab))
            {
                Entity playerEntity = em.Instantiate(prefabComponent.Prefab);
                Debug.Log($"[SpawnPlayer] Entity 생성 성공: {playerEntity}");

                em.SetComponentData(playerEntity, new Unity.Transforms.LocalTransform
                {
                    Position = pos,
                    Rotation = rot,
                    Scale = 1f
                });

                if (!em.HasComponent<PlayerInfo>(playerEntity))
                    em.AddComponent<PlayerInfo>(playerEntity);

                em.SetComponentData(playerEntity, new PlayerInfo { PlayerID = playerId });
                SetEntity(playerEntity, playerId);
            }
            else
            {
                Debug.LogError("[SpawnPlayer] Prefab Entity가 존재하지 않음!");
            }
        }
        else
        {
            Debug.LogError("[SpawnPlayer] EntityPrefab 컴포넌트를 찾을 수 없음!");
        }
        Debug.Log($"[PlayerManager] Spawned PlayerID:{playerId}");

        PlayerManager.Instance.playerEntityMap[playerId] = playerEntity;

        if (IsOwner)
        {
            Debug.Log("[PlayerNetwork] 내 플레이어로 설정");
            if (!em.HasComponent<IsMainPlayerTag>(playerEntity))
                em.AddComponent<IsMainPlayerTag>(playerEntity);

            //if (!em.HasComponent<FirstPersonPlayer>(playerEntity))
            //{
            //    em.AddComponentData(playerEntity, new FirstPersonPlayer
            //    {
            //        ControlledCharacter = playerEntity,
            //        LookInputSensitivity = 0.1f // 예시 값
            //    });
            //}

            //if (!em.HasComponent<FirstPersonPlayerInputs>(playerEntity))
            //{
            //    em.AddComponentData(playerEntity, new FirstPersonPlayerInputs());
            //}

            if (em.HasBuffer<LinkedEntityGroup>(playerEntity))
            {
                var linked = em.GetBuffer<LinkedEntityGroup>(playerEntity);
                if (linked.Length > 1)
                {
                    var cameraEntity = linked[1].Value;

                    if (!em.HasComponent<MainEntityCamera>(cameraEntity))
                        em.AddComponent<MainEntityCamera>(cameraEntity);

                    if (!em.HasComponent<FirstPersonCharacterView>(cameraEntity))
                    {
                        em.AddComponent<FirstPersonCharacterView>(cameraEntity);
                        em.SetComponentData(cameraEntity, new FirstPersonCharacterView
                        {
                            CharacterEntity = playerEntity
                        });
                    }
                }
            }

            Entity playerEntity_Controll = em.CreateEntity(typeof(FirstPersonPlayer), typeof(FirstPersonPlayerInputs));

            em.SetComponentData(playerEntity_Controll, new FirstPersonPlayer
            {
                ControlledCharacter = playerEntity,
                LookInputSensitivity = 0.2f // 예시 값
            });

            // 카메라 GameObject 활성화 (필요 시)
            transform.GetChild(1).gameObject.SetActive(true);
        }
        else
        {
            Debug.Log("[PlayerNetwork] 다른 플레이어로 설정");
            if (!em.HasComponent<IsOtherPlayerTag>(playerEntity))
                em.AddComponent<IsOtherPlayerTag>(playerEntity);
            // 카메라 GameObject 비활성화 (필요 시)
            transform.GetChild(1).gameObject.SetActive(false);
        }
    }

    [ClientRpc]
    public void InitPlayerClientRpc(ulong playerId, Vector3 pos, Quaternion rot)
    {
        ECSSpawn(playerId,pos,rot);
    }
    void FixedUpdate()
    {
        if (World.DefaultGameObjectInjectionWorld == null)
            return;
        if (entityManager == null || !entityManager.Exists(playerEntity)) return;

        if (IsOwner)
        {
            // 내 캐릭터: ECS가 메인, GameObject는 따라감
            // ECS Entity → GameObject Transform → NetworkTransform이 자동 전송
            var ecsTransform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(playerEntity);
            transform.position = ecsTransform.Position;
            transform.rotation = ecsTransform.Rotation;
        }
        else
        {
            // 다른 캐릭터: NetworkTransform으로 받은 데이터를 ECS로
            // NetworkTransform → GameObject Transform → ECS Entity
            entityManager.SetComponentData(playerEntity, new Unity.Transforms.LocalTransform
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = 1f
            });
        }
    }
}
