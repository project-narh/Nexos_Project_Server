using Unity.Entities;
using UnityEngine;
using Unity.Burst;
using Unity.Transforms;

public struct ActorEntity : IComponentData
{
    public enum ActorType
    {
        PLACEHOLDER = 0,
        Player,
        BasicNPC
    }

    public ActorType    actorType;
    public int          actorID;
}
public struct ActorEntityUninitialized : IComponentData
{
    // Tag component, flagged on bake. Destroyed after actorEntity is initialized
}
public struct ActorEntityInitialized : IComponentData
{
    // Tag component, created after actorEntity is initialized
}
public struct ActorEntityPlayerTag : IComponentData
{
    // Tag component, for designating the main camera's possesed entity
    // TODO : Look into this, if applying multiple tags is better than a single 'normal' IComponentData with a flag.
}

public class ActorEntityAuthoring : MonoBehaviour
{
    [SerializeField]
    private ActorEntity.ActorType   _actorType;

    public class Baker : Baker<ActorEntityAuthoring>
    {
        public override void Bake(ActorEntityAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ActorEntity
            {
                actorType = authoring._actorType
            });
            AddComponent(entity, new ActorEntityUninitialized());
        }
    }
}

public partial struct ActorEntityRegistrationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {

    }

    public void OnUpdate(ref SystemState state)
    {
        var entityCommandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach(var (actorEntity, uninitializedTag, entity) in SystemAPI.Query<RefRW<ActorEntity>,
                 RefRO<ActorEntityUninitialized>>().WithEntityAccess())
        {
            // register entity to ManagementSession's NativeArray register
            actorEntity.ValueRW.actorID = ManagementSession.Instance.RegisterActorEntity(entity);

            // remove & add appropriate tags
            entityCommandBuffer.RemoveComponent<ActorEntityUninitialized>(entity);
            entityCommandBuffer.AddComponent<ActorEntityInitialized>(entity);

            // tag for player entity
            if(actorEntity.ValueRO.actorType == ActorEntity.ActorType.Player)
            {
                entityCommandBuffer.AddComponent<ActorEntityPlayerTag>(entity);
            }
        }

        foreach(var (ptag, localTransform) in SystemAPI.Query<RefRO<ActorEntityPlayerTag>, RefRO<LocalTransform>>())
        {
            // TODO : REMOVE THIS
            //Camera.main.transform.position = localTransform.ValueRO.Position;
            //Camera.main.transform.rotation = localTransform.ValueRO.Rotation;

            ManagementSession.Instance.SetCameraRigPosition(localTransform.ValueRO.Position);
            ManagementSession.Instance.SetCameraRigRotation(localTransform.ValueRO.Rotation);
        }
    }
}