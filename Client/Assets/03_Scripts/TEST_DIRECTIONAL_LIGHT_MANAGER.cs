using UnityEngine;

public class TEST_DIRECTIONAL_LIGHT_MANAGER : MonoBehaviour
{
    private Light _directionalLight;

    [SerializeField]
    private Vector3 _rotation;

    private void Awake()
    {
        _directionalLight = GetComponent<Light>();
    }
    private void Update()
    {
        if (Input.GetKey(KeyCode.Alpha1))
        {
            transform.eulerAngles += _rotation * -Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.Alpha2))
        {
            transform.eulerAngles += _rotation * Time.deltaTime;
        }
    }
}
