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

        // Actor 엔티티 초기화
        foreach (var (actorEntity, uninitializedTag, entity) in
                SystemAPI.Query<RefRW<ActorEntity>, RefRO<ActorEntityUninitialized>>().WithEntityAccess())
        {
            // actor 등록
            actorEntity.ValueRW.actorID = ManagementSession.Instance.RegisterActorEntity(entity);

            // 태그 관리
            entityCommandBuffer.RemoveComponent<ActorEntityUninitialized>(entity);
            entityCommandBuffer.AddComponent<ActorEntityInitialized>(entity);

            // 플레이어 태그 추가
            if (actorEntity.ValueRO.actorType == ActorEntity.ActorType.Player)
            {
                Debug.Log($"[ActorEntityRegistration] Tagging player entity: {entity}");
                entityCommandBuffer.AddComponent<ActorEntityPlayerTag>(entity);

                // 메인 플레이어 태그도 추가 (필요한 경우)
                if (!SystemAPI.HasComponent<IsMainPlayerTag>(entity))
                {
                    Debug.Log($"[ActorEntityRegistration] Adding IsMainPlayerTag to player: {entity}");
                    entityCommandBuffer.AddComponent<IsMainPlayerTag>(entity);
                }
            }
        }

        // 카메라 위치 업데이트
        // IsMainPlayerTag와 함께 쿼리하여 메인 플레이어 카메라만 처리
        foreach (var (ptag, localTransform, _) in
                SystemAPI.Query<RefRO<ActorEntityPlayerTag>, RefRO<LocalTransform>, RefRO<IsMainPlayerTag>>())
        {
            Debug.Log($"[ActorEntityRegistration] Setting camera position to {localTransform.ValueRO.Position}");
            ManagementSession.Instance.SetCameraRigPosition(localTransform.ValueRO.Position);
            ManagementSession.Instance.SetCameraRigRotation(localTransform.ValueRO.Rotation);
        }
    }
}