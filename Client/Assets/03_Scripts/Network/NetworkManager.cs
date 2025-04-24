using UnityEngine;
using ServerCore;
using System;
using System.Net;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Collections;
using Unity.Jobs;
using JetBrains.Annotations;


public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    UDPConnect _conneter;

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

    public void StartUDP()
    {
        _conneter.UDPStart();
    }
    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _conneter = GetComponentInChildren<UDPConnect>();
            return;
        }
        Destroy(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _conneter.SetUDP(GetLocalIPAddress(), 8888);
        Application.runInBackground = true;
    }

    public UDPConnect Get_UDPconnect()
    {
        return _conneter;
    }

    void OnApplicationQuit()
    {
        try
        {
            // UDP 종료 처리
            if (_conneter != null)
            {
                // UDP 세션을 통해 종료 패킷 전송 (필요한 경우)
                C_LeaveGame leavePacket = new C_LeaveGame();
                ArraySegment<byte> segment = leavePacket.Write();
                _conneter.SendToServer(segment, (ushort)PacketID.C_LeaveGame);
                     
                // UDP 연결 종료
                _conneter.Close();
                UnityEngine.Debug.Log("UDP 연결 정상 종료");
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"연결 종료 중 오류 발생: {e.Message}");
        }
    }
}

[System.Serializable]
public class Disconnect
{
    bool disconnect = true;

}


