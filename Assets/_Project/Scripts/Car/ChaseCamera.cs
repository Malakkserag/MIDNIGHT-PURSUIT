using System;
using UnityEngine;

/// <summary>
/// Smooth third-person chase camera with switchable views (Close / Wide / Overhead).
/// Put on the Main Camera (NOT as a child of the car) and drag the car into Target.
/// Call NextView() (the HUD camera button or the C key) to cycle views.
/// </summary>
public class ChaseCamera : MonoBehaviour
{
    [Serializable]
    public struct View
    {
        public string name;
        public float distance;
        public float height;
        public float fieldOfView;
        [Tooltip("Driver view: camera sits inside the car at 'seat' (car-local) and looks straight ahead.")]
        public bool insideCar;
        public Vector3 seat;
    }

    private static readonly View DriverView = new View
    {
        name = "Driver", fieldOfView = 75f, insideCar = true, seat = new Vector3(-0.35f, 1.05f, 0.1f)
    };

    [SerializeField] private Transform target;
    [SerializeField] private View[] views =
    {
        new View { name = "Close",    distance = 8f,  height = 3f,  fieldOfView = 60f },
        new View { name = "Wide",     distance = 15f, height = 6f,  fieldOfView = 70f },
        new View { name = "Overhead", distance = 22f, height = 18f, fieldOfView = 65f },
        DriverView,
    };
    [SerializeField] private float lookAhead = 2f;
    [Tooltip("Lower = camera lags more behind turns.")]
    [SerializeField] private float rotationSmooth = 4f;
    [SerializeField] private float positionSmooth = 0.12f;
    [Tooltip("How fast the camera glides to a newly selected view.")]
    [SerializeField] private float viewBlendSpeed = 4f;

    private Vector3 velocity;
    private float currentYaw;
    private int viewIndex;
    private float distance, height, fov;
    private Camera cam;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    public string CurrentViewName => views.Length > 0 ? views[viewIndex].name : "";

    public void NextView()
    {
        if (views.Length == 0) return;
        viewIndex = (viewIndex + 1) % views.Length;
    }

    private void Start()
    {
        // Scenes saved before the Driver view existed only have 3 views: add it.
        bool hasDriver = false;
        foreach (View v in views) hasDriver |= v.insideCar;
        if (!hasDriver)
        {
            var list = new System.Collections.Generic.List<View>(views) { DriverView };
            views = list.ToArray();
        }

        cam = GetComponent<Camera>();
        if (target != null) currentYaw = target.eulerAngles.y;
        if (views.Length > 0)
        {
            distance = views[0].distance;
            height = views[0].height;
            fov = views[0].fieldOfView;
        }
    }

    private void LateUpdate()
    {
        if (target == null || views.Length == 0) return;

        // Glide toward the selected view.
        View v = views[viewIndex];
        float t = 1f - Mathf.Exp(-viewBlendSpeed * Time.deltaTime);
        distance = Mathf.Lerp(distance, v.distance, t);
        height = Mathf.Lerp(height, v.height, t);
        fov = Mathf.Lerp(fov, v.fieldOfView, t);
        if (cam != null)
        {
            cam.fieldOfView = fov;
            cam.nearClipPlane = v.insideCar ? 0.05f : 0.3f;
        }

        if (v.insideCar)
        {
            // Locked to the driver's seat, looking where the car points (level with the horizon).
            transform.position = target.TransformPoint(v.seat);
            Vector3 fwd = target.forward;
            fwd.y *= 0.3f;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            currentYaw = target.eulerAngles.y;
            velocity = Vector3.zero;
            return;
        }

        currentYaw = Mathf.LerpAngle(currentYaw, target.eulerAngles.y, rotationSmooth * Time.deltaTime);
        Quaternion yaw = Quaternion.Euler(0f, currentYaw, 0f);

        Vector3 desired = target.position + yaw * new Vector3(0f, height, -distance);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, positionSmooth);

        Vector3 lookPoint = target.position + target.forward * lookAhead + Vector3.up * 1f;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
    }
}
