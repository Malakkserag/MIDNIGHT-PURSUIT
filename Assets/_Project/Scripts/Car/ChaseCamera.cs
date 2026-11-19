using UnityEngine;

/// <summary>
/// Smooth third-person chase camera. Put on the Main Camera (NOT as a child of the car)
/// and drag the car into Target.
/// </summary>
public class ChaseCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 8f;
    [SerializeField] private float height = 3f;
    [SerializeField] private float lookAhead = 2f;
    [Tooltip("Lower = camera lags more behind turns.")]
    [SerializeField] private float rotationSmooth = 4f;
    [SerializeField] private float positionSmooth = 0.12f;

    private Vector3 velocity;
    private float currentYaw;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private void Start()
    {
        if (target != null) currentYaw = target.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        currentYaw = Mathf.LerpAngle(currentYaw, target.eulerAngles.y, rotationSmooth * Time.deltaTime);
        Quaternion yaw = Quaternion.Euler(0f, currentYaw, 0f);

        Vector3 desired = target.position + yaw * new Vector3(0f, height, -distance);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, positionSmooth);

        Vector3 lookPoint = target.position + target.forward * lookAhead + Vector3.up * 1f;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
    }
}
