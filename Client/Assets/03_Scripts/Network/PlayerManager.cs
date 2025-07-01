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
        GameObject go = Instantiate(prefab, pos, rot);
        var netObj = go.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, true);

        var playerSync = go.GetComponent<PlayerNetwork>();
        if (playerSync == null)
            playerSync = go.AddComponent<PlayerNetwork>();

        playerSync.InitPlayerClientRpc(playerId, pos, rot);
        Debug.Log($"[PlayerManager] 플레이어 생성");
    }
}

