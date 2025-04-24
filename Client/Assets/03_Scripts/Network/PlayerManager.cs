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


//public class PlayerManager : MonoBehaviour
//{
//    public static PlayerManager Instance { get; private set; }
//    public GameObject prefeb;
//    public Transform spawnPoint;
//    public Transform save;
//    public MainPlayer mainPlayer { get; private set; }



//    public S_LoginResponse login_data { private get; set; }

//    Dictionary<int, Player> playerList = new Dictionary<int, Player>(); // int : playerID UID로 바꾸려면 너무 많이 바꿔야 함 일단 playerID(세션ID)로 진행 
//    Dictionary<int, int > playerData = new Dictionary<int, int>(); // Key : UID  Value : playerId

//    private void Awake()
//    {
//        if (Instance == null)
//        {
//            Instance = this;
//            DontDestroyOnLoad(this.gameObject);
//            return;
//        }
//        else
//        {
//            PlayerManager.Instance.prefeb = prefeb;
//            PlayerManager.Instance.spawnPoint = spawnPoint;
//            PlayerManager.Instance.save = save;
//            GameObject.Destroy(this.gameObject);
//        }
//    }

//    public Player Get_player_ID(int playerid)
//    {
//        if(playerList.TryGetValue(playerid, out Player player))
//        {
//            return player;
//        }
//        return null;
//    }
//    public Player Get_player_UID(int UID)
//    {

//        if (playerList.TryGetValue(playerData[UID], out Player player))
//        {
//            return player;
//        }
//        return null;
//    }

//    public MainPlayer Get_MainPlayer()
//    {
//        return mainPlayer;
//    }

//    public void Player_List_wait(S_PlayerList listPacket)
//    {
//        StartCoroutine(Scene_Manager.Instance.LoadScene("Market", (callback) =>
//        {
//            PlayerManager.Instance.Player_Spawn(listPacket);
//        }));
//    }


//    //접속한 모든 플레이어 소환
//    public void Player_Spawn(S_PlayerList listPacket)
//    {
//        foreach (S_PlayerList.Player p in listPacket.players)
//        {
//            GameObject player = Instantiate(prefeb, p.position, p.rotation, save);
//            if (p.isSelf)
//            {
//                mainPlayer = player.AddComponent<MainPlayer>();
//                mainPlayer.playerId = p.playerId;
//                mainPlayer.uid = p.uid;
//                mainPlayer.Set_UID(p.uid);

//                if (login_data != null)
//                {
//                    mainPlayer.uid = login_data.uid;
//                    mainPlayer.nickname = login_data.nickname;
//                }
//                login_data = null;
//            }
//            else
//            {
//                Player n = player.AddComponent<Player>();
//                n.Move(p.position, p.rotation);
//                n.uid = p.uid;
//                n.playerId = p.playerId;
//                n.transform.position = p.position;
//                n.transform.rotation = p.rotation;
//                playerList.Add(p.playerId, n);
//                playerData.Add(p.uid, p.playerId);
//                //TODO : 추후 데이터 하나로 통일
//            }
//        }
//    }

//    //플레이어가 새로 접속했을때
//    public void PlayerEnter(S_BroadcastEnterGame packet)
//    {
//        if (mainPlayer != null)
//        {
//            if (playerList.ContainsKey(packet.uid) || (packet.playerId == mainPlayer.playerId)) 
//            {  
//                return; 

//            }
//            GameObject player = Instantiate(prefeb, packet.position, packet.rotation, save);
//            player.AddComponent<Player>();
//            Player p = player.GetComponent<Player>();
//            p.Move(packet.position, packet.rotation);
//            p.uid = packet.uid;
//            p.playerId = packet.playerId;
//            playerList.Add(packet.playerId, p);
//            playerData.Add(p.uid, p.playerId);
//        }
//        else
//        {
//            StartCoroutine(MainPlayerWait(packet));
//        }
//    }

//    IEnumerator MainPlayerWait(S_BroadcastEnterGame packet)
//    {
//        yield return new WaitUntil(() => mainPlayer != null);
//        //PlayerEnter(packet);
//    }


//    public void PlayerLeave(S_BroadcastLeaveGame packet)
//    {
//        if (mainPlayer.playerId == packet.playerId)
//        {
//            GameObject.Destroy(mainPlayer.gameObject);
//            mainPlayer = null;
//        }
//        else
//        {
//            if (playerList.TryGetValue(packet.playerId, out Player player))
//            {
//                GameObject.Destroy(player.gameObject);
//                playerData.Remove(playerList[packet.playerId].uid);
//                playerList.Remove(packet.playerId);
//            }
//        }
//    }
//    public void Move(S_BroadcastMove packet)
//    {
//        if (packet.playerId == mainPlayer.playerId) mainPlayer.Move(packet.position, packet.rotation);
//        else
//        {
//            if(playerList.TryGetValue(packet.playerId, out Player player))
//            {
//                player.Move(packet.position, packet.rotation);
//            }
//        }
//    }
//}
