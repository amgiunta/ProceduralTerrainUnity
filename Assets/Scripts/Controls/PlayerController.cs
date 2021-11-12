using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VoxelTerrain;

public class PlayerController : MonoBehaviour
{
    public bool moveable = true;
    public LayerMask terrainMask;

    public float moveSpeed;
    public float lookRotationSpeed;
    [Range(0, 1)] public float lookSensetivity = 1;
    public float deceleration = 1;
    public float maxRotation = 180;
    public float minRotation = 0;
    public float zoomDistance = 10f;
    public float maxZoomDistance = 1000f;
    public float minZoomDistance = 10f;
    [Range(0, 5)] public float zoomSensetivity = 1;

    private Camera gameCamera;
    private Vector3 movement;
    private bool deselerate = false;
    private Quaternion targetLook;
    private Vector3 targetEuler;

    void Awake() {
        gameCamera = GetComponent<Camera>();
    }

    // Start is called before the first frame update
    void Start()
    {
        moveable = true;
        Cursor.visible = false;
        targetLook = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    public bool TerrainAtCenter(out RaycastHit hit) {
        Ray ray = new Ray(transform.position, transform.forward);
        return Physics.Raycast(ray, out hit, maxZoomDistance, terrainMask);
    }

    public bool TerrainBelow(out RaycastHit hit) {
        Ray ray = new Ray(transform.position, Vector3.down);
        return Physics.SphereCast(ray, TerrainManager.instance.grid.voxelSize/2, out hit, maxZoomDistance * 2, terrainMask);
    }

    public void SetMovement(InputAction.CallbackContext context) {
        Vector2 moveDirectionXY = context.ReadValue<Vector2>().normalized;
        Vector3 moveDirectionXZ = new Vector3(moveDirectionXY.x, 0f, moveDirectionXY.y);

        if (moveDirectionXZ == Vector3.zero) {
            deselerate = true;
            return; 
        }

        deselerate = false;

        movement = moveDirectionXZ;
    }

    public void SetLookRotation(InputAction.CallbackContext context) {
        Vector2 lookDelta = context.ReadValue<Vector2>() * lookSensetivity;

        if (lookDelta == Vector2.zero) { return; }

        targetEuler = targetEuler + new Vector3(-lookDelta.y, lookDelta.x);
        targetEuler.x = Mathf.Clamp(targetEuler.x, -maxRotation, -minRotation);
    }

    public void SetZoom(InputAction.CallbackContext context) {
        Vector2 zoomDirection = context.ReadValue<Vector2>() * zoomSensetivity;

        zoomDistance = Mathf.Clamp(zoomDistance - zoomDirection.y, minZoomDistance, maxZoomDistance);
    }

    public void EditorToggle(InputAction.CallbackContext context) {
        if (!Application.isEditor) { return; }

        moveable = !moveable;
        Cursor.visible = !moveable;
    }

    private void MovePlayer() {
        if (!moveable) { return; }

        RotatePlayer();
        DistancePlayer();

        Vector3 forward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Quaternion forwardRotation = Quaternion.LookRotation(forward, Vector3.up);
        Debug.DrawRay(transform.position, forward * 10, Color.blue);
        Debug.DrawRay(transform.position, movement * 10, Color.yellow);
        Debug.DrawRay(transform.position, forwardRotation * movement * 10, Color.red);

        transform.position = Vector3.Lerp(transform.position, transform.position + forwardRotation * movement, moveSpeed * Time.fixedDeltaTime);
        Deselerate();
    }

    private void RotatePlayer() {
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(targetEuler), Time.fixedDeltaTime * lookRotationSpeed);
    }

    private void DistancePlayer() {
        RaycastHit terrainPoint;

        if (!TerrainBelow(out terrainPoint)) { return; }

        Vector3 backDir = Vector3.up;
        Vector3 targetPoint = terrainPoint.point + backDir * zoomDistance;

        Debug.DrawLine(terrainPoint.point, targetPoint, Color.yellow);

        transform.position = Vector3.Lerp(transform.position, targetPoint, Time.fixedDeltaTime * moveSpeed);
    }

    private void Deselerate() {
        if (movement == Vector3.zero || !deselerate) { return; }

        movement -= movement * deceleration;

        if (movement.magnitude <= 0.02f) {
            movement = Vector3.zero;
        }
    }
}
