//using Unity.Entities;
//using Unity.Mathematics;
//using Unity.Transforms;
//using UnityEngine;

//[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
//[UpdateAfter(typeof(FirstPersonPlayerFixedStepControlSystem))]
//public partial struct SendTransformToServerSystem : ISystem
//{
//    public void OnCreate(ref SystemState state)
//    {
//        state.RequireForUpdate(SystemAPI.QueryBuilder()
//            .WithAll<IsMainPlayerTag, LocalTransform, TargetTransform>()
//            .Build());
//    }

//    public void OnUpdate(ref SystemState state)
//    {
//        float threshold = 0.01f;

//        foreach (var (transform, target, entity) in SystemAPI
//                     .Query<RefRO<LocalTransform>, RefRW<TargetTransform>>()
//                     .WithAll<IsMainPlayerTag>()
//                     .WithEntityAccess())
//        {
//            float3 currentPos = transform.ValueRO.Position;
//            quaternion currentRot = transform.ValueRO.Rotation;

//            bool moved = math.distance(currentPos, target.ValueRO.Position) > threshold;
//            bool rotated = math.abs(math.dot(currentRot, target.ValueRO.Rotation)) < 0.9999f;

//            if (moved || rotated)
//            {
//                C_Move movePacket = new C_Move();
//                movePacket.position = new Vector3(currentPos.x, currentPos.y, currentPos.z);
//                movePacket.rotation = currentRot;

//                NetworkManager.Instance.Get_UDPconnect().SendToServer(movePacket.Write(), (ushort)PacketID.C_Move);

//                target.ValueRW.Position = currentPos;
//                target.ValueRW.Rotation = currentRot;
//            }
//        }
//    }
//}

//[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
//public partial struct ApplyTargetTransformSystem : ISystem
//{
//    public void OnCreate(ref SystemState state)
//    {
//        state.RequireForUpdate(SystemAPI.QueryBuilder()
//            .WithAll<TargetTransform, LocalTransform>()
//            .WithNone<IsMainPlayerTag>()
//            .Build());
//    }

//    public void OnUpdate(ref SystemState state)
//    {
//        float dt = SystemAPI.Time.DeltaTime;
//        float moveSpeed = 10f;

//        foreach (var (localTransform, target) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<TargetTransform>>())
//        {
//            float3 currentPos = localTransform.ValueRO.Position;
//            quaternion currentRot = localTransform.ValueRO.Rotation;

//            float3 targetPos = target.ValueRO.Position;
//            quaternion targetRot = target.ValueRO.Rotation;

//            float distance = math.distance(currentPos, targetPos); // 거리차이
//            float angle = math.degrees(math.acos(math.clamp(math.dot(currentRot, targetRot), -1f, 1f))) * 2f; // 각도

//            if (distance > 0.01f)
//            {
//                localTransform.ValueRW.Position = math.lerp(currentPos, targetPos, dt * moveSpeed);
//            }

//            if (angle > 0.002f)
//            {
//                float t = math.clamp(dt * moveSpeed, 0.001f, 1f);
//                localTransform.ValueRW.Rotation = math.slerp(currentRot, targetRot, t);
//            }
//        }
//    }
//}