using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementBehaviour : MonoBehaviour
{
    //movement variables
    Vector3 moveDirection;
    Vector3 zDirection;
    Vector3 xDirection;
    float verticalVelocity;

    float forwardInput;
    float rightInput;

    [Header("Movement variables")]
    public float movementSpeed;
    public float rotationSpeed;
    public float tiltSpeed;
    public float dodgeSpeed;
    public float gravity;

    float forwardVelocity = 0f;
    float tiltAngle;

    //IK variables
    private Vector3 rightFootPosition, leftFootPosition;
    private Vector3 rightFootIKPosition, leftFootIKPosition;
    private Quaternion rightFootIKRotation, leftFootIKRotation;
    private float lastPelvisPositionY, lastRightFootPositionY, lastLeftFootPositionY;

    [Header("Inverse Kinematics Variables")]
    public bool enableFeetIK = true;
    public bool enablePelvisShift = false;
    public bool enableAccelerationTilt = true;
    [SerializeField] private float heightFromGroundRaycast = 1.5f;
    [SerializeField] private float raycastDownDistance = 1.5f;
    [SerializeField] private LayerMask levelLayer;
    [SerializeField] private float pelvisOffset = 0f;
    [SerializeField] private float pelvisUpAndDownSpeed = 0.3f;
    [SerializeField] private float feetToIKPositionSpeed = 0.5f;

    public string rightFootAnimVariableName = "RightFootCurve";
    public string leftFootAnimVariableName = "LeftFootCurve";

    public bool useFeetRotation;
    public bool showSolverDebug;

    //public references
    Animator anim;
    CharacterController charController;
    Camera cam;

    //trial
    Quaternion collisionDirection;
    private bool dodging;
    public bool enableDodging;

    // Start is called before the first frame update
    void Start()
    {
        cam = Camera.main;   
        anim = GetComponent<Animator>();
        charController = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        DoLocomotion();
        SetAnimations();
    }

    #region Locomotions
    void DoLocomotion(){
        moveDirection = Vector3.zero;

        rightInput = Input.GetAxis("Vertical");
        forwardInput = Input.GetAxis("Horizontal");

        zDirection = cam.transform.forward;
        xDirection = cam.transform.right;
        zDirection.y = xDirection.y = 0f;
        zDirection.Normalize();
        xDirection.Normalize();
 
        moveDirection = xDirection * forwardInput + zDirection * rightInput; 
        
        //turn radius
        //moveDirection += transform.forward * steeringRadius;

       
        moveDirection *= movementSpeed;
        forwardVelocity = Vector3.Dot(moveDirection, transform.forward);

        if(moveDirection != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDirection), rotationSpeed);

        //Debug.DrawLine(transform.position, moveDirection + transform.position + Vector3.up, Color.yellow, Time.deltaTime);
        Debug.DrawLine(transform.position, (moveDirection + transform.position) + Vector3.up, Color.yellow, Time.deltaTime);

        if(charController.isGrounded){
            verticalVelocity = -gravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
            Mathf.Clamp(verticalVelocity, 0f, 100f);
        }
        moveDirection.y = verticalVelocity;
       
        charController.Move(moveDirection * Time.deltaTime);
    }

    

    void SetAnimations(){
        if(forwardVelocity == 0f){
            anim.SetBool("Locomotion", false);
        }
        else
        {
            anim.SetBool("Locomotion", true);
            anim.SetFloat("Locomotion Velocity", forwardVelocity);
        }
    }

    #endregion

    
    //FeetGrounding
    private void FixedUpdate() {
        if(enableFeetIK == false){return;} //do nothing if feature disabled
        if(anim == null){return;}   //do nothing if animator not found

        AdjustFeetTarget(ref rightFootPosition, HumanBodyBones.RightFoot); //Adjust feet IK for right foot
        AdjustFeetTarget(ref leftFootPosition, HumanBodyBones.LeftFoot);  //Adjust feet IK for left foot

        //shoot raycast and solve position of IK
        SolveFeetPositon(rightFootPosition, ref rightFootIKPosition, ref rightFootIKRotation); 
        SolveFeetPositon(leftFootPosition, ref leftFootIKPosition, ref leftFootIKRotation); 

        
    }

    private void OnAnimatorIK(int layerIndex) {
        if(enableFeetIK == false){return;}
        if(anim == null){return;}

        if(enableAccelerationTilt){
            if(forwardVelocity > 3f){
                Quaternion hipTilt = Quaternion.FromToRotation(Vector3.up, -(moveDirection + transform.position) + Vector3.up);
                Quaternion currentHipTilt = Quaternion.Slerp(anim.GetBoneTransform(HumanBodyBones.Hips).localRotation, hipTilt, tiltSpeed);
                //print(characterTurnDirection);
                TiltHip(currentHipTilt);
            }
            else
            {
                Quaternion currentHipTilt = Quaternion.Slerp(anim.GetBoneTransform(HumanBodyBones.Hips).localRotation, Quaternion.identity, tiltSpeed);
                //print(characterTurnDirection);
                TiltHip(currentHipTilt);
            }
        }

        //dodging
        if(enableDodging){
            if(dodging){
                Quaternion spineTilt = Quaternion.FromToRotation(transform.forward, collisionDirection * transform.forward);
                Quaternion currentSpineTilt = Quaternion.Slerp(anim.GetBoneTransform(HumanBodyBones.Chest).localRotation, spineTilt, dodgeSpeed); //tilt chest to face obstacle
                MoveSpine(currentSpineTilt);
            }
            else
            {
                Quaternion currentHipTilt = Quaternion.Slerp(anim.GetBoneTransform(HumanBodyBones.Chest).localRotation, Quaternion.identity, tiltSpeed);
                //print(characterTurnDirection);
                MoveSpine(currentHipTilt);
            }
        }

        if(enablePelvisShift)
            MovePelvisHeight(); //tilt and shift pelvis to match legs

        anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);  //blend weights for IK are 1 by default
        if(useFeetRotation){
            anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, anim.GetFloat(rightFootAnimVariableName));
        }

        MoveFeetToIKPoint(AvatarIKGoal.RightFoot, rightFootIKPosition, rightFootIKRotation, ref lastRightFootPositionY);

        
        anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
        if(useFeetRotation){
            anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, anim.GetFloat(leftFootAnimVariableName));
        }

        MoveFeetToIKPoint(AvatarIKGoal.LeftFoot, leftFootIKPosition, leftFootIKRotation, ref lastLeftFootPositionY);
    }

    //test
    void TiltHip(Quaternion rotateHipTo){
        anim.SetBoneLocalRotation(HumanBodyBones.Hips, rotateHipTo);
    }

    #region FeetIK
    //FeetGroundingMethods

    
    void MoveFeetToIKPoint(AvatarIKGoal foot, Vector3 positionIKHolder, Quaternion rotationIKHolder, ref float lastFootPositionY){
        Vector3 targetIKPosition = anim.GetIKPosition(foot);
        
        if(positionIKHolder != Vector3.zero){
            targetIKPosition = transform.InverseTransformPoint(targetIKPosition);
            positionIKHolder = transform.InverseTransformPoint(positionIKHolder);

            float lastFootHeight = Mathf.Lerp(lastFootPositionY, positionIKHolder.y, feetToIKPositionSpeed); //store height at which animation places foot bone
            targetIKPosition.y += lastFootHeight; //adjust for change in height

            lastFootPositionY = lastFootHeight; //feed back temp value after changes

            targetIKPosition = transform.TransformPoint(targetIKPosition); //convert to world space
            anim.SetIKRotation(foot, rotationIKHolder); //give new foot rotation to anim system
        }
        
        anim.SetIKPosition(foot, targetIKPosition); //give new foot position to anim system
    }

    private void MovePelvisHeight(){
        if(rightFootIKPosition == Vector3.zero || leftFootIKPosition == Vector3.zero || lastPelvisPositionY == 0){
            lastPelvisPositionY = anim.bodyPosition.y;
            return;
        }

        float lOffsetPosition = leftFootIKPosition.y - transform.position.y;
        float rOffsetPosition = rightFootIKPosition.y - transform.position.y;

        float totalOffest = (lOffsetPosition < rOffsetPosition) ? lOffsetPosition : rOffsetPosition;

        Vector3 newPelvisPosition = anim.bodyPosition + Vector3.up * totalOffest;

        newPelvisPosition.y = Mathf.Lerp(lastPelvisPositionY, newPelvisPosition.y, pelvisUpAndDownSpeed);

        anim.bodyPosition = newPelvisPosition;
        lastPelvisPositionY = anim.bodyPosition.y;
    }

    //locate feet position using raycasts to solve new position for IK
    private void SolveFeetPositon(Vector3 fromSkyPosition, ref Vector3 feetIKPosition, ref Quaternion feetIKRotations){
        RaycastHit feetOutHit;

        if(showSolverDebug)
            Debug.DrawLine(fromSkyPosition, fromSkyPosition + Vector3.down * (raycastDownDistance + heightFromGroundRaycast), Color.black); 

        if(Physics.Raycast(fromSkyPosition, Vector3.down, out feetOutHit, raycastDownDistance + heightFromGroundRaycast, levelLayer)){
            feetIKPosition = fromSkyPosition;
            feetIKPosition.y = feetOutHit.point.y + pelvisOffset;
            feetIKRotations = Quaternion.FromToRotation(Vector3.up, feetOutHit.normal) * transform.rotation;
            return;
        }

        feetIKPosition = Vector3.zero; // return feet to local origin if no ground found
    }

    private void AdjustFeetTarget(ref Vector3 feetPositions, HumanBodyBones foot){
        feetPositions = anim.GetBoneTransform(foot).position;
        feetPositions.y = transform.position.y + heightFromGroundRaycast;
    }
    #endregion

    private void OnTriggerStay(Collider other) {
        if(other.tag == "Projectile"){
            dodging = true;
            collisionDirection = Quaternion.FromToRotation(transform.forward, other.transform.position - transform.position);
            // Quaternion currentSpineTilt = Quaternion.Slerp(anim.GetBoneTransform(HumanBodyBones.Spine).localRotation, collisionDirection, dodgeSpeed);
        }
    }

    private void OnTriggerExit(Collider other) {
        if(other.tag == "Projectile"){
            dodging = false;
        }
    }
   

    private void MoveSpine(Quaternion spineTilt){
        anim.SetBoneLocalRotation(HumanBodyBones.Spine, spineTilt);
    }

    private void ResetSpine(){
        anim.SetBoneLocalRotation(HumanBodyBones.Spine, Quaternion.identity);
    }

}
