using UnityEngine;

public class GiantArmIKDriver : MonoBehaviour
{
    [Header("Actual joints")]
    public Transform upperArm;
    public Transform forearm;
    public Transform wrist;

    [Header("IK inputs")]
    public Transform wristTarget;
    public Transform elbowPole;

    [Header("Control")]
    public bool solveIK = true;
    public bool matchWristRotation = true;

    [Header("Read only during testing")]
    [SerializeField] private string solverStatus;
    [SerializeField] private float wristError;

    private float upperArmLength;
    private float forearmLength;
    private bool initialized;

    private void Start()
    {
        if (upperArm == null || forearm == null || wrist == null || wristTarget == null || elbowPole == null)
        {
            solverStatus = "Missing references";
            return;
        }

        upperArmLength = Vector3.Distance(upperArm.position,forearm.position);

        forearmLength = Vector3.Distance(forearm.position,wrist.position);

        if (upperArmLength < 0.00001f || forearmLength < 0.00001f)
        {
            solverStatus = "Invalid bone length";
            return;
        }

        initialized = true;
        solverStatus = "Ready";
    }

    private void LateUpdate()
    {
        if (!initialized || !solveIK) return;

        bool success = TwoBoneSolver.TrySolve(
            upperArm.position,
            wristTarget.position,
            elbowPole.position,
            upperArmLength,
            forearmLength,
            out Vector3 solvedElbow,
            out string message
        );

        solverStatus = message;

        if (!success)
        {
            wristError = Vector3.Distance(wrist.position,wristTarget.position);
            return;
        }

        Vector3 currentUpperDirection =
            forearm.position - upperArm.position;

        Vector3 desiredUpperDirection =
            solvedElbow - upperArm.position;

        Quaternion upperCorrection = Quaternion.FromToRotation(
            currentUpperDirection,
            desiredUpperDirection
        );

        upperArm.rotation = upperCorrection * upperArm.rotation;

        Vector3 currentForearmDirection = wrist.position - forearm.position;

        Vector3 desiredForearmDirection = wristTarget.position - forearm.position;

        Quaternion forearmCorrection = Quaternion.FromToRotation(currentForearmDirection,desiredForearmDirection
        );

        forearm.rotation = forearmCorrection * forearm.rotation;

        if (matchWristRotation)
        {
            wrist.rotation = wristTarget.rotation;
        }

        wristError = Vector3.Distance(wrist.position,wristTarget.position);
    }
}