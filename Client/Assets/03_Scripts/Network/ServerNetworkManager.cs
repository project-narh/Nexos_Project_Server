using UnityEngine;
using System;
using System.Net;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Collections;
using Unity.Jobs;
using JetBrains.Annotations;
using Unity.Netcode;


public class ServerNetworkManager : MonoBehaviour
{
    public static ServerNetworkManager Instance { get; private set; }


    public bool isServer = false; // 서버 여부

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogError("[ServerNetworkManager] ServerNetworkManager 인스턴스가 중복 생성되었습니다.");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            if (isServer)
            {
                //NetworkManager.Singleton.StartServer();
                NetworkManager.Singleton.StartHost();
                Debug.Log("[ServerNetworkManager] Netcode 서버가 실행되었습니다.");
            }
            else
            {
                NetworkManager.Singleton.StartClient();
                Debug.Log("[ServerNetworkManager] 클라이언트로 접속하였습니다.");
            }
        }
    }


    public static string GetLocalIPAddress()
    {
        string localIP = "Not found";

        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                localIP = ip.ToString();
                Debug.Log($"Local IP Address: {localIP}");
                break;
            }
        }

        return localIP;
    }

  
}

[System.Serializable]
public class Disconnect
{
    bool disconnect = true;

}


