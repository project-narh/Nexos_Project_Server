using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;
using Unity.Burst.Intrinsics;
using System.Collections.Generic;
using UnityEngine;

[UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct FirstPersonCharacterPhysicsUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private FirstPersonCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                FirstPersonCharacterComponent,
                FirstPersonCharacterControl>()
            .Build(ref state);

        _context = new FirstPersonCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(_characterQuery);
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

        FirstPersonCharacterPhysicsUpdateJob job = new FirstPersonCharacterPhysicsUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct FirstPersonCharacterPhysicsUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public FirstPersonCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;

        void Execute(FirstPersonCharacterAspect characterAspect)
        {
            characterAspect.PhysicsUpdate(ref Context, ref BaseContext);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
        {
        }
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(FirstPersonPlayerVariableStepControlSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct FirstPersonCharacterVariableUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private FirstPersonCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    private static Dictionary<Entity, float3> lastPositions = new();
    private static Dictionary<Entity, quaternion> lastRotations = new();
    private float elapsedTime;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        elapsedTime = 0;
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                FirstPersonCharacterComponent,
                FirstPersonCharacterControl>()
            .Build(ref state);

        _context = new FirstPersonCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(_characterQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

        FirstPersonCharacterVariableUpdateJob variableUpdateJob = new FirstPersonCharacterVariableUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        //variableUpdateJob.ScheduleParallel();

        FirstPersonCharacterViewJob viewJob = new FirstPersonCharacterViewJob
        {
            FirstPersonCharacterLookup = SystemAPI.GetComponentLookup<FirstPersonCharacterComponent>(true),
        };
        //viewJob.ScheduleParallel();
        var handle1 = variableUpdateJob.ScheduleParallel(state.Dependency);
        var handle2 = viewJob.ScheduleParallel(handle1);
        state.Dependency = handle2;

        elapsedTime += SystemAPI.Time.DeltaTime;
        if (elapsedTime < 0.1f) return;
        elapsedTime = 0f;

        var em = state.EntityManager;

        var query = SystemAPI.QueryBuilder()
            .WithAll<LocalTransform, IsMainPlayerTag>()
            .Build();

        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var lastTransformLookup = SystemAPI.GetComponentLookup<LastTransform>(false);

        var entities = query.ToEntityArray(Allocator.Temp);

        foreach (var entity in entities)
        {
            if (!transformLookup.HasComponent(entity))
                continue;

            var transform = transformLookup[entity];
            float3 pos = transform.Position;
            quaternion rot = transform.Rotation;

            bool changed = true;

            if (lastTransformLookup.HasComponent(entity))
            {
                var last = lastTransformLookup[entity];
                float posDiff = math.distance(pos, last.Position);
                float rotDiff = math.degrees(math.acos(math.clamp(math.dot(rot, last.Rotation), -1f, 1f)));

                changed = posDiff > 0.001f || rotDiff > 0.5f;
            }

            if (changed)
            {
                var movePacket = new C_Move
                {
                    position = new Vector3(pos.x, pos.y, pos.z),
                    rotation = new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w)
                };

                NetworkManager.Instance.Get_UDPconnect().SendToServer(movePacket.Write(), (ushort)PacketID.C_Move);

                if (lastTransformLookup.HasComponent(entity))
                {
                    lastTransformLookup[entity] = new LastTransform { Position = pos, Rotation = rot };
                }
                else
                {
                    em.AddComponentData(entity, new LastTransform { Position = pos, Rotation = rot });
                }
            }
        }
        entities.Dispose();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct FirstPersonCharacterVariableUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public FirstPersonCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;

        void Execute(FirstPersonCharacterAspect characterAspect)
        {
            characterAspect.VariableUpdate(ref Context, ref BaseContext);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
        { }
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct FirstPersonCharacterViewJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<FirstPersonCharacterComponent> FirstPersonCharacterLookup;

        void Execute(ref LocalTransform localTransform, in FirstPersonCharacterView characterView)
        {
            if (FirstPersonCharacterLookup.HasComponent(characterView.CharacterEntity) &&
                FirstPersonCharacterLookup.TryGetComponent(characterView.CharacterEntity, out var character))
            {
                localTransform.Rotation = character.ViewLocalRotation;
            }
        }
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct OtherPlayerLerpSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (transform, target) in SystemAPI
            .Query<RefRW<LocalTransform>, RefRO<TargetTransform>>()
            .WithAll<IsOtherPlayerTag>())
        {
            transform.ValueRW.Position = math.lerp(transform.ValueRW.Position, target.ValueRO.Position, dt * 10f);
            transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, target.ValueRO.Rotation, dt * 10f);
        }
    }
}
