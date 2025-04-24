using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


public struct PlayerInfo : IComponentData
{
    public int PlayerId;
    public int Uid;
}

public static class PlayerSpawner
{
    public static Entity SpawnPlayer(
        EntityManager em,
        Entity prefab,
        int playerId,
        int uid,
        float3 pos,
        quaternion rot,
        bool isSelf)
    {
        if (prefab == Entity.Null)
            return Entity.Null;
        Debug.Log($"[SpawnPlayer] Spawning player with ID: {playerId}, Position: {pos}, IsSelf: {isSelf}");

        // 프리팹 계층 구조 확인 (디버깅)
        if (em.HasComponent<LinkedEntityGroup>(prefab))
        {
            var linkedEntities = em.GetBuffer<LinkedEntityGroup>(prefab);
            Debug.Log($"[SpawnPlayer] Prefab has {linkedEntities.Length} linked entities");
        }

        Entity entity = em.Instantiate(prefab);
        em.SetComponentData(entity, new LocalTransform
        {
            Position = pos,
            Rotation = rot,
            Scale = 1f
        });

        // 플레이어 정보 설정
        em.AddComponentData(entity, new PlayerInfo
        {
            PlayerId = playerId,
            Uid = uid
        });

        // 중요: 모든 이동 및 물리 관련 컴포넌트 초기화
        if (em.HasComponent<KinematicCharacterBody>(entity))
        {
            var characterBody = em.GetComponentData<KinematicCharacterBody>(entity);
            characterBody.RelativeVelocity = float3.zero; // 상대 속도 초기화
            characterBody.IsGrounded = true;              // 착지 상태로 설정
            em.SetComponentData(entity, characterBody);
        }

        if (em.HasComponent<FirstPersonCharacterControl>(entity))
        {
            var control = em.GetComponentData<FirstPersonCharacterControl>(entity);
            control.MoveVector = float3.zero;            // 이동 벡터 초기화
            control.LookDegreesDelta = float2.zero;      // 시선 변화 초기화
            control.Jump = false;                        // 점프 비활성화
            em.SetComponentData(entity, control);
        }
        // Relevant 컴포넌트 설정 (층 관리)
        if (em.HasComponent<Relevant>(entity))
        {
            var relevant = em.GetComponentData<Relevant>(entity);
            relevant.currentFloor = 1; // 기본 층 설정
            em.SetComponentData(entity, relevant);
            Debug.Log($"[SpawnPlayer] Setting Relevant floor to {relevant.currentFloor}");
        }
        else
        {
            em.AddComponentData(entity, new Relevant
            {
                currentFloor = 1 // 기본 층 설정
            });
            Debug.Log("[SpawnPlayer] Added Relevant component with floor 1");
        }

        if (em.HasComponent<FirstPersonCharacterComponent>(entity))
        {
            var character = em.GetComponentData<FirstPersonCharacterComponent>(entity);
            // 필요한 속성 초기화
            em.SetComponentData(entity, character);
        }
        em.SetComponentData(entity, new LocalTransform
        {
            Position = pos,
            Rotation = rot,
            Scale = 1f
        });

        em.AddComponentData(entity, new PlayerInfo
        {
            PlayerId = playerId,
            Uid = uid
        });
        // ActorEntity 설정 (필요한 경우)
        if (em.HasComponent<ActorEntity>(entity))
        {
            var actorEntity = em.GetComponentData<ActorEntity>(entity);

            // 메인 플레이어인 경우 Player 타입으로 설정
            if (isSelf)
            {
                actorEntity.actorType = ActorEntity.ActorType.Player;
            }

            em.SetComponentData(entity, actorEntity);
            Debug.Log($"[SpawnPlayer] Updated ActorEntity type to {actorEntity.actorType}");
        }
        if (isSelf)
        {
            em.AddComponent<IsMainPlayerTag>(entity);
            // ActorEntityPlayerTag 추가 (카메라 제어용)
            if (!em.HasComponent<ActorEntityPlayerTag>(entity))
            {
                em.AddComponent<ActorEntityPlayerTag>(entity);
                Debug.Log("[SpawnPlayer] Added ActorEntityPlayerTag to main player");
            }

            // 뷰 설정 처리
            if (em.HasComponent<FirstPersonCharacterComponent>(entity))
            {
                // 계층 구조에서 뷰 엔티티 찾기
                Entity viewEntity = FindViewEntityInHierarchy(em, entity);

                if (viewEntity != Entity.Null)
                {
                    var characterComponent = em.GetComponentData<FirstPersonCharacterComponent>(entity);
                    characterComponent.ViewEntity = viewEntity;
                    em.SetComponentData(entity, characterComponent);

                    Debug.Log($"[SpawnPlayer] Set ViewEntity for FirstPersonCharacterComponent: {viewEntity}");

                    // 뷰 엔티티에도 캐릭터 참조 설정
                    if (!em.HasComponent<FirstPersonCharacterView>(viewEntity))
                    {
                        em.AddComponentData(viewEntity, new FirstPersonCharacterView
                        {
                            CharacterEntity = entity
                        });
                    }
                    else
                    {
                        var view = em.GetComponentData<FirstPersonCharacterView>(viewEntity);
                        view.CharacterEntity = entity;
                        em.SetComponentData(viewEntity, view);
                    }
                }
                else
                {
                    Debug.LogWarning("[SpawnPlayer] Could not find view entity in hierarchy!");
                }
            }
        }
        else
        {
            em.AddComponent<IsOtherPlayerTag>(entity);
            em.AddComponentData(entity, new TargetTransform
            {
                Position = pos,
                Rotation = rot
            });
            // 다른 플레이어의 경우 입력 관련 컴포넌트 제거
            if (em.HasComponent<FirstPersonPlayer>(entity))
            {
                em.RemoveComponent<FirstPersonPlayer>(entity);
            }

            if (em.HasComponent<FirstPersonPlayerInputs>(entity))
            {
                em.RemoveComponent<FirstPersonPlayerInputs>(entity);
            }

            if (em.HasComponent<FirstPersonCharacterControl>(entity))
            {
                // 컨트롤 컴포넌트는 제거하지 않고 초기화만 진행
                var control = em.GetComponentData<FirstPersonCharacterControl>(entity);
                control.MoveVector = float3.zero;
                control.LookDegreesDelta = float2.zero;
                control.Jump = false;
                em.SetComponentData(entity, control);
            }
            if (em.HasComponent<FirstPersonCharacterComponent>(entity))
            {
                // 1. 완전히 제거하는 방법
                // em.RemoveComponent<FirstPersonCharacterComponent>(entity);

                // 2. 또는 특정 기능만 비활성화하는 방법
                var character = em.GetComponentData<FirstPersonCharacterComponent>(entity);
                // 필요에 따라 특정 값 조정 (예: 입력 감도를 0으로 설정)
                character.ViewPitchDegrees = 0f;
                em.SetComponentData(entity, character);
            }

            // 카메라나 뷰 관련 컴포넌트도 처리
            if (em.HasComponent<FirstPersonCharacterView>(entity))
            {
                em.RemoveComponent<FirstPersonCharacterView>(entity);
            }
        }

        Debug.Log($"[SpawnPlayer] Instantiating: {prefab} / isSelf: {isSelf}"); 
        return entity;
    }
    // 계층 구조에서 뷰 엔티티 찾기
    private static Entity FindViewEntityInHierarchy(EntityManager em, Entity rootEntity)
    {
        if (!em.HasComponent<LinkedEntityGroup>(rootEntity))
        {
            Debug.LogWarning($"[FindViewEntityInHierarchy] No LinkedEntityGroup on entity: {rootEntity}");
            return Entity.Null;
        }

        var linkedEntities = em.GetBuffer<LinkedEntityGroup>(rootEntity);
        Debug.Log($"[FindViewEntityInHierarchy] Searching through {linkedEntities.Length} entities");

        // 자식 엔티티 중에서 특정 조건을 만족하는 뷰 엔티티 찾기
        for (int i = 1; i < linkedEntities.Length; i++) // 0번은 자기 자신
        {
            Entity childEntity = linkedEntities[i].Value;

            // FirstPersonCharacterViewAuthoring이 적용된 엔티티 찾기
            // 이 엔티티는 특정 컴포넌트나 태그를 가지고 있을 것입니다
            if (em.HasComponent<FirstPersonCharacterView>(childEntity))
            {
                Debug.Log($"[FindViewEntityInHierarchy] Found view entity: {childEntity}");
                return childEntity;
            }

            // 다른 방법: Parent 컴포넌트로 부모-자식 관계 확인
            if (em.HasComponent<Parent>(childEntity))
            {
                var parent = em.GetComponentData<Parent>(childEntity);
                if (parent.Value == rootEntity)
                {
                    // 여기서 추가 조건 확인 가능
                    // 예: 특정 컴포넌트 존재 여부, 이름 패턴 등

                    Debug.Log($"[FindViewEntityInHierarchy] Found potential view entity: {childEntity}");
                    return childEntity;
                }
            }
        }

        Debug.LogWarning("[FindViewEntityInHierarchy] No view entity found");
        return Entity.Null;
    }
    public static void DespawnPlayer(EntityManager em, int uid)
    {
        EntityQuery query = em.CreateEntityQuery(typeof(PlayerInfo));
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        using var infos = query.ToComponentDataArray<PlayerInfo>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < infos.Length; i++)
        {
            if (infos[i].Uid == uid)
            {
                em.DestroyEntity(entities[i]);
                break;
            }
        }
    }
}
