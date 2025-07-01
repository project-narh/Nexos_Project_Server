using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    public static PlayerManager Instance;
    bool isSpawn = false;
    private Dictionary<int, Entity> playerEntityMap = new();
    int nextID = 1;
    public GameObject prefab;
    public GameObject entityPrefab;
    public Entity prefabEntity;
    public Entity mainPlayerEntity { get; private set; }
    public Vector3 spawnPoint;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            StartCoroutine(InitPrefab());
        }
        else
        {
            Debug.LogError("[PlayerManager] PlayerManager 인스턴스가 중복 생성되었습니다.");
            Destroy(gameObject);
            return;
        }
        
    }


    IEnumerator WaitForEntityPrefab()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Sub Scene 로드 대기
        while (true)
        {
            var query = em.CreateEntityQuery(typeof(EntityPrefab));
            if (query.CalculateEntityCount() > 0)
            {
                prefabEntity = query.GetSingleton<EntityPrefab>().Prefab;
                Debug.Log("[PlayerManager] EntityPrefab 로드 완료");
                break;
            }

            Debug.Log("[PlayerManager] EntityPrefab 대기 중...");
            yield return new WaitForSeconds(0.1f);
        }
    }

    IEnumerator InitPrefab()
    {
        yield return StartCoroutine(WaitForEntityPrefab());
        if (ServerNetworkManager.Instance.isServer)
        {
            // 현재 접속중인 클라이언트 처리
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                OnClientConnected(clientId);
            }

            // 이후 새로 접속하는 클라이언트 처리
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            Debug.Log("[PlayerManager] OnClientConnectedCallback 등록 완료");
        }
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[PlayerManager] 클라이언트 접속: {clientId}");
        int playerID = nextID++;

        
        SpawnPlayer(clientId, playerID, spawnPoint, Quaternion.identity);

    }

    void SpawnPlayer(ulong clientId, int playerId, Vector3 pos, Quaternion rot)
    {
        bool isSelf = (clientId == NetworkManager.Singleton.LocalClientId);
        // [Netcode Instantiate + Spawn]
        GameObject go = Instantiate(prefab, pos, rot);
        var netObj = go.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, true);

        // [ECS Entity 생성: BakerAuthoring으로 자동 생성됨]
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        Debug.Log($"[SpawnPlayer] 시작 - ClientId: {clientId}, PlayerId: {playerId}");
        Debug.Log($"[SpawnPlayer] World 존재: {World.DefaultGameObjectInjectionWorld != null}");
        Debug.Log($"[SpawnPlayer] EntityManager 존재: {em != null}");

        //Entity playerEntity = em.Instantiate(prefabEntity);

        // EntityPrefab 상태 확인
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

                if (isSelf && go != null)
                {
                    var playerSync = go.GetComponent<PlayerNetwork>();
                    if (playerSync == null)
                    {
                        playerSync = go.AddComponent<PlayerNetwork>();
                    }
                    go.transform.GetChild(1).gameObject.SetActive(true); // 카메라 활성화
                    playerSync.SetEntity(playerEntity);
                }

                // Main / Other 플레이어 구분
                if (isSelf)
                {
                    em.AddComponent<IsMainPlayerTag>(playerEntity);
                    mainPlayerEntity = playerEntity;

                    if (em.HasBuffer<LinkedEntityGroup>(playerEntity))
                    {
                        var linkedEntities = em.GetBuffer<LinkedEntityGroup>(playerEntity);

                        // 첫번째 자식 Entity (index 1) 기준
                        if (linkedEntities.Length > 1)
                        {
                            var cameraEntity = linkedEntities[1].Value;

                            if (!em.HasComponent<MainEntityCamera>(cameraEntity))
                                em.AddComponent<MainEntityCamera>(cameraEntity);

                            if (!em.HasComponent<FirstPersonCharacterView>(cameraEntity))
                            {
                                em.AddComponent<FirstPersonCharacterView>(cameraEntity); // 컴포넌트 추가
                                em.SetComponentData(cameraEntity, new FirstPersonCharacterView
                                {
                                    CharacterEntity = playerEntity
                                });
                            }
                        }
                    }

                }
                else
                {
                    em.AddComponent<IsOtherPlayerTag>(playerEntity);
                    // 입력 관련 컴포넌트 제거
                    if (em.HasComponent<FirstPersonPlayer>(playerEntity))
                        em.RemoveComponent<FirstPersonPlayer>(playerEntity);
                    if (em.HasComponent<FirstPersonPlayerInputs>(playerEntity))
                        em.RemoveComponent<FirstPersonPlayerInputs>(playerEntity);
                }

                playerEntityMap[playerId] = playerEntity;
                go.GetComponent<PlayerNetwork>().SetEntity(playerEntity);
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
        Debug.Log($"[PlayerManager] Spawned PlayerID:{playerId}, IsSelf:{isSelf}");
    }
}

