using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public class ManagementSession : MonoBehaviour
{
    #region [ Initialize singleton ] ----------------------------

    private static ManagementSession _instance;
    public static ManagementSession Instance
    {
        get
        {
            if (_instance == null) return null;
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else Destroy(this.gameObject);

        DontDestroyOnLoad(gameObject);

        InitializeSession();
    }

    #endregion

    #region [ Session variables ] ----------------------------

    // Settings
    [SerializeField]
    private int                 _actorEntityArraySize = 20;

    // References
    [SerializeField]
    private Transform           _cameraRigTransform;

    // Variables
    public NativeArray<Entity>  actorEntityArray;
    private int                 actorEntityArrayCursor = 0;

    #endregion

    #region [ Main loop ] ----------------------------

    private void InitializeSession()
    {
        actorEntityArray = new NativeArray<Entity>(_actorEntityArraySize, Allocator.Persistent);
    }

    #endregion

    #region [ Utilities ] ----------------------------

    public int RegisterActorEntity(Entity entity)
    {
        int currentCursor = actorEntityArrayCursor;
        actorEntityArrayCursor++;

        actorEntityArray[currentCursor] = entity;
        Debug.Log("ManagementSession // Registered new entity ["
            + actorEntityArray[currentCursor] + "] as ID " + currentCursor);

        return currentCursor;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            //Debug.Log(actorEntityArray[0]);
        }
    }

    public Transform GetCameraRigTransform()
    {
        return _cameraRigTransform;
    }

    // these may look cumbersome, but can prove useful when multiple threads race for this
    public void SetCameraRigPosition(float3 newPosition)
    {
        _cameraRigTransform.position = newPosition;
    }

    public void SetCameraRigRotation(Quaternion newRotation)
    {
        _cameraRigTransform.rotation = newRotation;
    }

    #endregion
}
