
using ServerCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;

 public enum PacketID // 지금은 이렇게 하드코딩 하지만 나중에는 자동화
{
    C_EnterGame = 1,
	S_BroadcastEnterGame = 2,
	C_LeaveGame = 3,
	S_BroadcastLeaveGame = 4,
	S_PlayerList = 5,
	C_Move = 6,
	S_BroadcastMove = 7,
	C_AccountLogin = 8,
	S_LoginResponse = 9,
	C_Register = 10,
	S_RegisterResponse = 11,
	S_Ack = 12,
	C_Ack = 13,
	KeepAlive = 14,
	
}

public interface IPacket
{ 
	ushort Protocol { get; }
	void Read(ArraySegment<byte> segment);
	ArraySegment<byte> Write();
}


public class C_EnterGame : IPacket
{
    public ushort sequenceNumber;
    public int uid;     

	public ushort Protocol { get { return (ushort) PacketID.C_EnterGame; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.uid = BitConverter.ToInt32(s.Slice(count, s.Length - count));
count += sizeof(int);
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.C_EnterGame);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.uid);
count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return new ArraySegment<byte>(segment.Array, segment.Offset, count);
    }
}

public class S_BroadcastEnterGame : IPacket
{
    public ushort sequenceNumber;
    public int uid;


public int playerId;


public Vector3 position;


public Quaternion rotation;     

	public ushort Protocol { get { return (ushort) PacketID.S_BroadcastEnterGame; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.uid = BitConverter.ToInt32(s.Slice(count, s.Length - count));
count += sizeof(int);this.playerId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
count += sizeof(int);this.position.x = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.position.y = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.position.z = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);this.rotation.x = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.y = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.z = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.w = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.S_BroadcastEnterGame);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.uid);
count += sizeof(int);success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerId);
count += sizeof(int);success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.x);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.y);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.z);
count += sizeof(float);success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.x);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.y);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.z);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.w);
count += sizeof(float);
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class C_LeaveGame : IPacket
{
    public ushort sequenceNumber;
         

	public ushort Protocol { get { return (ushort) PacketID.C_LeaveGame; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.C_LeaveGame);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class S_BroadcastLeaveGame : IPacket
{
    public ushort sequenceNumber;
    public int playerId;     

	public ushort Protocol { get { return (ushort) PacketID.S_BroadcastLeaveGame; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.playerId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
count += sizeof(int);
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.S_BroadcastLeaveGame);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerId);
count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class S_PlayerList : IPacket
{
    public ushort sequenceNumber;  // 패킷 헤더 (시퀀스 번호)
    public List<Player> players = new List<Player>();

    public ushort Protocol { get { return (ushort)PacketID.S_PlayerList; } }

    public class Player
    {
        public bool isSelf;
        public int playerId;
        public int uid;
        public Vector3 position;
        public Quaternion rotation;

        // 🔹 플레이어 데이터 읽기 (안전한 오프셋 검증 추가)
        public void Read(ReadOnlySpan<byte> s, ref ushort count)
        {
            try
            {
                if (count + sizeof(bool) + sizeof(int) * 2 + sizeof(float) * 7 > s.Length)
                {
                    throw new Exception("잘못된 패킷 크기: 데이터가 부족함");
                }

                this.isSelf = BitConverter.ToBoolean(s.Slice(count, sizeof(bool)));
                count += sizeof(bool);

                this.playerId = BitConverter.ToInt32(s.Slice(count, sizeof(int)));
                count += sizeof(int);

                this.uid = BitConverter.ToInt32(s.Slice(count, sizeof(int)));
                count += sizeof(int);

                this.position.x = BitConverter.ToSingle(s.Slice(count, sizeof(float)));
                count += sizeof(float);
                this.position.y = BitConverter.ToSingle(s.Slice(count, sizeof(float)));
                count += sizeof(float);
                this.position.z = BitConverter.ToSingle(s.Slice(count, sizeof(float)));
                count += sizeof(float);

                this.rotation.x = BitConverter.ToSingle(s.Slice(count, sizeof(float)));
                count += sizeof(float);
                this.rotation.y = BitConverter.ToSingle(s.Slice(count, sizeof(float)));
                count += sizeof(float);
                this.rotation.z = BitConverter.ToSingle(s.Slice(count, sizeof(float)));
                count += sizeof(float);
                this.rotation.w = BitConverter.ToSingle(s.Slice(count, sizeof(float)));
                count += sizeof(float);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[패킷 오류] 플레이어 데이터 읽기 실패: {e.Message}");
            }
        }

        // 🔹 플레이어 데이터 쓰기 (버퍼 크기 초과 방지 추가)
        public bool Write(Span<byte> s, ref ushort count)
        {
            try
            {
                if (count + sizeof(bool) + sizeof(int) * 2 + sizeof(float) * 7 > s.Length)
                {
                    Console.WriteLine("[패킷 오류] 버퍼 초과 발생");
                    return false;
                }

                bool success = true;
                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(bool)), this.isSelf);
                count += sizeof(bool);

                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(int)), this.playerId);
                count += sizeof(int);
                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(int)), this.uid);
                count += sizeof(int);

                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(float)), this.position.x);
                count += sizeof(float);
                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(float)), this.position.y);
                count += sizeof(float);
                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(float)), this.position.z);
                count += sizeof(float);

                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(float)), this.rotation.x);
                count += sizeof(float);
                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(float)), this.rotation.y);
                count += sizeof(float);
                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(float)), this.rotation.z);
                count += sizeof(float);
                success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(float)), this.rotation.w);
                count += sizeof(float);

                return success;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[패킷 오류] 플레이어 데이터 쓰기 실패: {e.Message}");
                return false;
            }
        }
    }

    // 🔹 전체 패킷 읽기
    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;
        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        ushort size = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));  // size
        count += sizeof(ushort);

        ushort packetID = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));  // packet ID
        count += sizeof(ushort);

        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));  // sequence number
        count += sizeof(ushort);

        ushort playerCount = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));  // 플레이어 수
        count += sizeof(ushort);

        this.players.Clear();
        for (int i = 0; i < playerCount; i++)
        {
            Player player = new Player();
            player.Read(s, ref count);
            this.players.Add(player);
        }
    }

    // 🔹 전체 패킷 쓰기
    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0;
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);  // size 자리 확보
        success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(ushort)), (ushort)PacketID.S_PlayerList);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(ushort)), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, sizeof(ushort)), (ushort)this.players.Count);
        count += sizeof(ushort);

        foreach (var player in players)
        {
            success &= player.Write(s, ref count);
        }

        // 패킷 크기 기록
        success &= BitConverter.TryWriteBytes(s.Slice(0, sizeof(ushort)), count);
        if (!success)
        {
            Console.WriteLine("[패킷 오류] 데이터 쓰기 실패");
            return null;
        }

        return SendBufferHelper.Close(count);
    }
}


public class C_Move : IPacket
{
    public ushort sequenceNumber;
    public Vector3 position;


public Quaternion rotation;     

	public ushort Protocol { get { return (ushort) PacketID.C_Move; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.position.x = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.position.y = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.position.z = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);this.rotation.x = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.y = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.z = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.w = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.C_Move);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.x);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.y);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.z);
count += sizeof(float);success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.x);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.y);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.z);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.w);
count += sizeof(float);
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class S_BroadcastMove : IPacket
{
    public ushort sequenceNumber;
    public int playerId;


public Vector3 position;


public Quaternion rotation;     

	public ushort Protocol { get { return (ushort) PacketID.S_BroadcastMove; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.playerId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
count += sizeof(int);this.position.x = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.position.y = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.position.z = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);this.rotation.x = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.y = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.z = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
this.rotation.w = BitConverter.ToSingle(s.Slice(count, s.Length - count));
count += sizeof(float);
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.S_BroadcastMove);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerId);
count += sizeof(int);success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.x);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.y);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.position.z);
count += sizeof(float);success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.x);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.y);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.z);
count += sizeof(float);
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.rotation.w);
count += sizeof(float);
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class C_AccountLogin : IPacket
{
    public ushort sequenceNumber;
    public string player;


public string user_Pw;     

	public ushort Protocol { get { return (ushort) PacketID.C_AccountLogin; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        ushort playerLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
this.player = Encoding.Unicode.GetString(s.Slice(count, playerLen));
count += playerLen;ushort user_PwLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
this.user_Pw = Encoding.Unicode.GetString(s.Slice(count, user_PwLen));
count += user_PwLen;
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.C_AccountLogin);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        ushort playerLen = (ushort)Encoding.Unicode.GetBytes(this.player, 0, this.player.Length, segment.Array, segment.Offset + count + sizeof(ushort));
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), playerLen);
count += sizeof(ushort);
count += playerLen;ushort user_PwLen = (ushort)Encoding.Unicode.GetBytes(this.user_Pw, 0, this.user_Pw.Length, segment.Array, segment.Offset + count + sizeof(ushort));
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), user_PwLen);
count += sizeof(ushort);
count += user_PwLen;
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class S_LoginResponse : IPacket
{
    public ushort sequenceNumber;
    public int uid;


public int playerId;


public string nickname;     

	public ushort Protocol { get { return (ushort) PacketID.S_LoginResponse; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.uid = BitConverter.ToInt32(s.Slice(count, s.Length - count));
count += sizeof(int);this.playerId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
count += sizeof(int);ushort nicknameLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
this.nickname = Encoding.Unicode.GetString(s.Slice(count, nicknameLen));
count += nicknameLen;
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.S_LoginResponse);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.uid);
count += sizeof(int);success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerId);
count += sizeof(int);ushort nicknameLen = (ushort)Encoding.Unicode.GetBytes(this.nickname, 0, this.nickname.Length, segment.Array, segment.Offset + count + sizeof(ushort));
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), nicknameLen);
count += sizeof(ushort);
count += nicknameLen;
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class C_Register : IPacket
{
    public ushort sequenceNumber;
    public string user_Id;


public string user_Pw;


public string nickname;     

	public ushort Protocol { get { return (ushort) PacketID.C_Register; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        ushort user_IdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
this.user_Id = Encoding.Unicode.GetString(s.Slice(count, user_IdLen));
count += user_IdLen;ushort user_PwLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
this.user_Pw = Encoding.Unicode.GetString(s.Slice(count, user_PwLen));
count += user_PwLen;ushort nicknameLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
this.nickname = Encoding.Unicode.GetString(s.Slice(count, nicknameLen));
count += nicknameLen;
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.C_Register);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        ushort user_IdLen = (ushort)Encoding.Unicode.GetBytes(this.user_Id, 0, this.user_Id.Length, segment.Array, segment.Offset + count + sizeof(ushort));
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), user_IdLen);
count += sizeof(ushort);
count += user_IdLen;ushort user_PwLen = (ushort)Encoding.Unicode.GetBytes(this.user_Pw, 0, this.user_Pw.Length, segment.Array, segment.Offset + count + sizeof(ushort));
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), user_PwLen);
count += sizeof(ushort);
count += user_PwLen;ushort nicknameLen = (ushort)Encoding.Unicode.GetBytes(this.nickname, 0, this.nickname.Length, segment.Array, segment.Offset + count + sizeof(ushort));
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), nicknameLen);
count += sizeof(ushort);
count += nicknameLen;
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class S_RegisterResponse : IPacket
{
    public ushort sequenceNumber;
    public bool isSuccess;


public string msg;     

	public ushort Protocol { get { return (ushort) PacketID.S_RegisterResponse; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.isSuccess = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
count += sizeof(bool);ushort msgLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
this.msg = Encoding.Unicode.GetString(s.Slice(count, msgLen));
count += msgLen;
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0; 
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.S_RegisterResponse);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.isSuccess);
count += sizeof(bool);ushort msgLen = (ushort)Encoding.Unicode.GetBytes(this.msg, 0, this.msg.Length, segment.Array, segment.Offset + count + sizeof(ushort));
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), msgLen);
count += sizeof(ushort);
count += msgLen;
        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class S_Ack : IPacket
{
    public ushort sequenceNumber;
    public bool type; // false - 요청, true - 응답

    public ushort Protocol { get { return (ushort)PacketID.S_Ack; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;
        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.type = BitConverter.ToBoolean(s.Slice(count, sizeof(bool)));
        count += sizeof(bool);
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0;
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.S_Ack);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.type);
        count += sizeof(bool);

        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class C_Ack : IPacket
{
    public ushort sequenceNumber;
    public bool type; // false - 요청, true - 응답

    public ushort Protocol { get { return (ushort)PacketID.C_Ack; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;
        ReadOnlySpan<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, sizeof(ushort)));
        count += sizeof(ushort);
        this.type = BitConverter.ToBoolean(s.Slice(count, sizeof(bool)));
        count += sizeof(bool);
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0;
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.C_Ack);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.type);
        count += sizeof(bool);

        success &= BitConverter.TryWriteBytes(s, count);
        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

public class KeepAlive : IPacket
{
    public ushort sequenceNumber;
    public int sessionID; //

    public ushort Protocol { get { return (ushort)PacketID.KeepAlive; } }

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;
        ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort); // size
        count += sizeof(ushort); // packetID
        this.sequenceNumber = BitConverter.ToUInt16(s.Slice(count, 2));
        count += sizeof(ushort);
        this.sessionID = BitConverter.ToInt32(s.Slice(count, 4));
        count += sizeof(int);
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0;
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort); // size 자리 확보
        success &= BitConverter.TryWriteBytes(s.Slice(count, 2), (ushort)PacketID.KeepAlive);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, 2), this.sequenceNumber);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, 4), this.sessionID);
        count += sizeof(int);

        success &= BitConverter.TryWriteBytes(s.Slice(0, 2), count);

        if (!success)
            return null;
        return SendBufferHelper.Close(count);
    }
}

