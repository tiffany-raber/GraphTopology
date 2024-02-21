using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityStandardAssets.CrossPlatformInput;
using VRStandardAssets.Utils;

public class LM_MovementController : MonoBehaviour
{
    [Header("Controls")]
    public KeyCode translateForward = KeyCode.UpArrow;
    public KeyCode translateBackward = KeyCode.DownArrow;
    public KeyCode translateLeft = KeyCode.None;
    public KeyCode translateRight = KeyCode.None;
    public KeyCode yawLeft = KeyCode.LeftArrow;
    public KeyCode yawRight = KeyCode.RightArrow;
    public KeyCode pitchUp = KeyCode.None;
    public KeyCode pitchDown = KeyCode.None;
    public bool mouseLook = false;
    [Header("Parameters")]
    public float walkSpeed = 3;
    public float runSpeed = 10;
    private float speed;
    [Tooltip("degrees per second")] public float rotSpeed = 60f; // 60 deg/s = 10 rev/min
    public float pitchSensitivity = 1.0f;
    public bool decoupleHead;
    public bool clampVerticalRotation;
    public float minimumX;
    public float maximumX;

    private Camera cam;
    private CollisionFlags m_CollisionFlags;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponentInChildren<Camera>();
    }

    // Update is called once per frame
    // Put any getbuttondown calls here so they don't get missed
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.LeftShift)) speed = runSpeed;
        else speed = walkSpeed;

        // Handle translations
        var tx = (float)(Convert.ToDouble(Input.GetKey(translateRight)) - Convert.ToDouble(Input.GetKey(translateLeft)));
        var tz = (float)(Convert.ToDouble(Input.GetKey(translateForward)) - Convert.ToDouble(Input.GetKey(translateBackward)));
        var motor = transform.GetComponent<CharacterController>();

        var m_Input = new Vector2(tx, tz);

        // normalize input if it exceeds 1 in combined length:
        if (m_Input.sqrMagnitude > 1) m_Input.Normalize();

        // Modified from FirstPersonController.cs
        Vector3 desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;
        // get a normal for the surface that is being touched to move along it
        RaycastHit hitInfo;
        Physics.SphereCast(transform.position, motor.radius, Vector3.down, out hitInfo,
                           motor.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

        Vector3 moveDir = Vector3.zero;
        moveDir.x = desiredMove.x;
        moveDir.z = desiredMove.z;

        if (motor.enabled)
        {
            m_CollisionFlags = motor.Move(moveDir * speed * Time.fixedDeltaTime);
        }


        // Handle rotations
        var yaw = (float)(Convert.ToDouble(Input.GetKey(yawRight)) - Convert.ToDouble(Input.GetKey(yawLeft)));
        var pitch = (float)(Convert.ToDouble(Input.GetKey(pitchUp)) - Convert.ToDouble(Input.GetKey(pitchDown)));
        var roll = 0f;
        var m_CharacterTargetRot = transform.localRotation;
        var m_CameraTargetRot = cam.transform.localRotation;
        if (decoupleHead)
        {
            m_CameraTargetRot *= Quaternion.Euler(-pitch * pitchSensitivity * Time.fixedDeltaTime,
                                                  yaw * rotSpeed * Time.fixedDeltaTime,
                                                  roll);

            if (clampVerticalRotation) m_CameraTargetRot = ClampRotationAroundXAxis(m_CameraTargetRot);
            cam.transform.localRotation = m_CameraTargetRot;
        }
        else
        {
            m_CharacterTargetRot *= Quaternion.Euler(0f, yaw * rotSpeed * Time.fixedDeltaTime, roll);
            m_CameraTargetRot *= Quaternion.Euler(-pitch * pitchSensitivity * Time.fixedDeltaTime, 0f, roll);

            if (clampVerticalRotation) m_CameraTargetRot = ClampRotationAroundXAxis(m_CameraTargetRot);

            transform.localRotation = m_CharacterTargetRot;
            cam.transform.localRotation = m_CameraTargetRot;
        }
    }


    // From FirstPersonController.cs
    Quaternion ClampRotationAroundXAxis(Quaternion q)
    {
        q.x /= q.w;
        q.y /= q.w;
        q.z /= q.w;
        q.w = 1.0f;

        float angleX = 2.0f * Mathf.Rad2Deg * Mathf.Atan(q.x);

        angleX = Mathf.Clamp(angleX, minimumX, maximumX);

        q.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleX);

        return q;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        //dont move the rigidbody if the character is on top of it
        if (m_CollisionFlags == CollisionFlags.Below)
        {
            return;
        }

        if (body == null || body.isKinematic)
        {
            return;
        }
        body.AddForceAtPosition(transform.GetComponent<CharacterController>().velocity * 0.1f, hit.point, ForceMode.Impulse);
    }
}
