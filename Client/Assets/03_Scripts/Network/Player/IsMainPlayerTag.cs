using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct IsMainPlayerTag : IComponentData { }

public struct IsOtherPlayerTag : IComponentData { }

public struct TargetTransform : IComponentData
{
    public float3 Position;
    public quaternion Rotation;
}

//if (em.HasComponent<TargetTransform>(entity))
//{
//    em.SetComponentData(entity, new TargetTransform
//    {
//        Position = new float3(packet.position.x, packet.position.y, packet.position.z),
//        Rotation = new quaternion(packet.rotation.x, packet.rotation.y, packet.rotation.z, packet.rotation.w)
//    });
//}