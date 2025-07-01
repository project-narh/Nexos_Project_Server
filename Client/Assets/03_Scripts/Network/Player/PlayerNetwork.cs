using UnityEngine;
using Unity.Netcode;
using Unity.Entities;

public class PlayerNetwork : NetworkBehaviour
{
    private Entity playerEntity;
    private EntityManager entityManager;

    private void Awake()
    {
        enabled = false;
    }

    public void SetEntity(Entity entity)
    {
        playerEntity = entity;
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        enabled = true;
    }

    void Update()
    {
        if (World.DefaultGameObjectInjectionWorld == null)
            return;
        if (entityManager == null || !entityManager.Exists(playerEntity)) return;

        if (IsOwner)
        {
            // 내 캐릭터: ECS가 메인, GameObject는 따라감
            // ECS Entity → GameObject Transform → NetworkTransform이 자동 전송
            var ecsTransform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(playerEntity);
            transform.position = ecsTransform.Position;
            transform.rotation = ecsTransform.Rotation;
        }
        else
        {
            // 다른 캐릭터: NetworkTransform으로 받은 데이터를 ECS로
            // NetworkTransform → GameObject Transform → ECS Entity
            entityManager.SetComponentData(playerEntity, new Unity.Transforms.LocalTransform
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = 1f
            });
        }
    }
}
