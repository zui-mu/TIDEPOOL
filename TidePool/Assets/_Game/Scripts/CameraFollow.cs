using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 6.7f;
    [SerializeField] private float lookHeight = .5f;
    [SerializeField] private float mouseSensitivity = .15f;
    [SerializeField] private float minPitch = 5f;
    [SerializeField] private float maxPitch = 75f;
    [Header("Scroll zoom")]
    [SerializeField] private bool enableScrollZoom;
    [SerializeField, Min(1.1f)] private float minZoomDistance = 1.8f;
    [SerializeField, Min(1.1f)] private float maxZoomDistance = 7.5f;
    [SerializeField, Min(.01f)] private float scrollZoomStep = .65f;
    private float preferredDistance;
    [Header("Camera obstruction")]
    [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;
    [SerializeField,Min(.05f)] private float collisionRadius = .25f;
    [SerializeField,Min(.01f)] private float wallPadding = .06f;
    [SerializeField,Min(.02f)] private float recoveryTime = .2f;
    private float yaw,pitch,currentDistance,recoveryVelocity;
    private Camera viewCamera;
    private RaycastHit[] hits = new RaycastHit[64];
    private Renderer[] targetRenderers;
    private ShadowCastingMode[] originalShadowModes;
    private bool hidden;
    public bool IsObstructed {get;private set;}
    public float ResolvedDistance => currentDistance;
    public float CollisionRadius {get;private set;}

    private void Start()
    {
        yaw=transform.eulerAngles.y;
        pitch=Mathf.Clamp(Mathf.DeltaAngle(0,transform.eulerAngles.x),minPitch,maxPitch);
        preferredDistance = distance;
        currentDistance=distance;viewCamera=GetComponent<Camera>();
        if(target)
        {
            targetRenderers=target.GetComponentsInChildren<Renderer>();
            originalShadowModes=new ShadowCastingMode[targetRenderers.Length];
            for(int i=0;i<targetRenderers.Length;i++) originalShadowModes[i]=targetRenderers[i].shadowCastingMode;
        }
    }
    public bool IsCameraObstacle(Collider collider)
    {
        return collider && !collider.isTrigger && collider.transform!=target && !collider.transform.IsChildOf(target);
    }
    private void LateUpdate()
    {
        if(!target)return;
        var mouse=Mouse.current;
        if(mouse!=null && mouse.rightButton.isPressed)
        {
            Vector2 delta=mouse.delta.ReadValue();yaw+=delta.x*mouseSensitivity;
            pitch=Mathf.Clamp(pitch-delta.y*mouseSensitivity,minPitch,maxPitch);
        }
        if (enableScrollZoom && mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                if (Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer &&
                    InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)
                    scroll /= 120f;
                preferredDistance = Mathf.Clamp(preferredDistance - scroll * scrollZoomStep,
                    minZoomDistance, Mathf.Max(minZoomDistance, maxZoomDistance));
            }
        }

        Vector3 focus=target.position+Vector3.up*lookHeight;
        Quaternion rotation=Quaternion.Euler(pitch,yaw,0);
        Vector3 direction=rotation*Vector3.back;
        // Protect the whole near clipping rectangle, not only the camera's centre point.
        float radius=collisionRadius;
        if(viewCamera)
        {
            float halfHeight=viewCamera.nearClipPlane*Mathf.Tan(viewCamera.fieldOfView*.5f*Mathf.Deg2Rad);
            radius=Mathf.Max(radius,halfHeight*Mathf.Sqrt(1+viewCamera.aspect*viewCamera.aspect)+.025f);
        }
        CollisionRadius=radius;
        float requestedDistance = enableScrollZoom ? preferredDistance : distance;
        // Check the camera's current position too while it is easing toward a closer zoom.
        float castDistance = enableScrollZoom ? Mathf.Max(requestedDistance, currentDistance) : requestedDistance;
        float safeDistance = castDistance;
        int count=Physics.SphereCastNonAlloc(focus,radius,direction,hits,castDistance,collisionLayers,QueryTriggerInteraction.Ignore);
        RaycastHit[] results=hits;
        if(count==hits.Length)
        {
            results=Physics.SphereCastAll(focus,radius,direction,castDistance,collisionLayers,QueryTriggerInteraction.Ignore);
            count=results.Length;
        }
        for(int i=0;i<count;i++)
            if(IsCameraObstacle(results[i].collider))safeDistance=Mathf.Min(safeDistance,Mathf.Max(0,results[i].distance-wallPadding));
        IsObstructed = safeDistance < requestedDistance - .01f;
        // Obstructions pull the camera in immediately. Intentional zoom and recovery ease in.
        if(safeDistance<currentDistance){currentDistance=safeDistance;recoveryVelocity=0;}
        else currentDistance=Mathf.SmoothDamp(currentDistance,Mathf.Min(requestedDistance,safeDistance),ref recoveryVelocity,recoveryTime);
        currentDistance=Mathf.Min(currentDistance,safeDistance);
        transform.SetPositionAndRotation(focus+direction*currentDistance,rotation);
        SetHidden(currentDistance<1.05f);
    }
    private void SetHidden(bool value)
    {
        if(hidden==value || targetRenderers==null)return;
        hidden=value;
        // This cooperates with DryRegionCameraGuard's independent forceRenderingOff flag.
        for(int i=0;i<targetRenderers.Length;i++)
            if(targetRenderers[i])targetRenderers[i].shadowCastingMode=value?ShadowCastingMode.ShadowsOnly:originalShadowModes[i];
    }
    private void OnDisable(){SetHidden(false);}
}
