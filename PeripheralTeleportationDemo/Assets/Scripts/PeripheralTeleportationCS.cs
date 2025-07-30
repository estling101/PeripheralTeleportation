using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class PeripheralTeleportationCS : RestrictorControllerBase
{
    // Specific Parameters
    [Header("The two auxilary cameras and their rigs")]
    public GameObject auxRig0, auxRig1;
    public Camera auxCam0Left, auxCam0Right, auxCam1Left, auxCam1Right;
    [Header("Peripheral Teleportation parameters")]
    [Tooltip("Time interval for each teleport")]
    public float teleportInterval = 1f;
    public float periScale = 1.0f; // The scale of the environment in the periphery
    public float periRotScale = 1.0f; // The scale of the environment in the periphery
    [Tooltip("Postural Sway Threshold")]
    public float swayThreshold = .01f;
    [Header("Dither")]
    public bool ditherSpace = true;
    public bool ditherTime = false;
    [Header("Locomotion Controller")] // The locomotion system
    public ActionBasedContinuousMoveProvider moveProvider;
    public ActionBasedContinuousTurnProvider turnProvider;

    // Only for testing and figure illustration
    public bool pausePT = false;
    public bool showPT0 = false;
    public bool showPT1 = false;

    protected int 
        PTLerp, 
        PTPTCam0, 
        PTPTCam1,
        PTDitherSpace,
        PTDitherTime;
    protected int
        csEyeIndex; 

    protected float transSpeed, turnSpeed; // turn speed: deg/s
    // This timer is for PT camera image lerping only
    protected float timer = 0;
    protected bool isRenderingLeft = false; // In multi pass rendering, indicate which eye is currently being rendered.
    //protected bool switchBuffer = false; // switch the buffer that stores the prev frame when pt is updated
    protected float turnSpeedRad;
    protected Vector3 ampCam0Bias = new Vector3(0, 0, 0);
    protected Vector3 ampCam1Bias = new Vector3(0, 0, 0);
    protected Quaternion ampCam0RotBias = Quaternion.identity;
    protected Quaternion ampCam1RotBias = Quaternion.identity;
    protected Vector3[] predicts; // contains four vectors: w/o translation, w/o rotation.
    protected Vector3 prevDeltaCamLocalPos = Vector3.zero;

    protected RenderTexture tex0Left, tex0Right, tex1Left, tex1Right;
    //protected RenderTexture tex0LeftPT, tex0RightPT, tex1LeftPT, tex1RightPT, texMCLeftPT, texMCRightPT;
    //protected RenderTexture tex0LeftPTD, tex0RightPTD, tex1LeftPTD, tex1RightPTD, texMCLeftPTD, texMCRightPTD;

    protected override void TSStart()
    {
        transSpeed = moveProvider.moveSpeed;
        turnSpeed = turnProvider.turnSpeed;
        PredictVector();
        // Property IDs
        PTLerp = Shader.PropertyToID("_Lerp");
        PTPTCam0 = Shader.PropertyToID("PTCam0");
        PTPTCam1 = Shader.PropertyToID("PTCam1");
        PTDitherSpace = Shader.PropertyToID("_DitherSpace");
        computeShaderPostProcessing.SetBool(PTDitherSpace, ditherSpace);
        PTDitherTime = Shader.PropertyToID("_DitherTime");
        computeShaderPostProcessing.SetBool(PTDitherTime, ditherTime);

        csEyeIndex = Shader.PropertyToID("_EyeIndex");

        if (forceBlackPeriphery)
        {
            SetBlackPeriphery(true);
            SetDitherSpace(false);
        }
    }

    void PredictVector()
    {
        predicts = new Vector3[4];
        // no translation, no rotation
        predicts[0] = new Vector3(0, 0, 0);
        // no translation, only rotation
        predicts[1] = new Vector3(0, 0, 0);
        // only translation, no rotation
        predicts[2] = new Vector3(0, 0, transSpeed * teleportInterval); // forward
        // both translation and rotation
        turnSpeedRad = turnSpeed / 180f * Mathf.PI;
        float r = transSpeed / turnSpeedRad;
        predicts[3] = new Vector3(0, (1 - Mathf.Cos(turnSpeedRad * teleportInterval)) * r, Mathf.Sin(turnSpeedRad * teleportInterval) * r); // right

    }

    void InitCamera(Camera c, int depth, RenderTexture rt, StereoTargetEyeMask eyeMask)
    {
        // Copy the parameters from the main camera
        // If play before the HMD is turned on, the size of render texture is incorrect.
        c.CopyFrom(mainCam);
        // Everything that involves camera properties should be set after copy
        c.depth = depth;
        c.forceIntoRenderTexture = true;

        // The aux cameras seems to be fixed after copyfrom()
        // I was suspecting if it is because the project matrix was modified
        c.ResetStereoProjectionMatrices();
        c.ResetWorldToCameraMatrix();
        c.ResetStereoViewMatrices();
        c.stereoTargetEye = eyeMask;
        // Let aux cameras to render to render textures
        c.targetTexture = rt;
        // don't let the camera jitter
        c.useJitteredProjectionMatrixForTransparentRendering = false;
    }

    void InitRenderTextures()
    {
        RenderTextureDescriptor desc = GetXREyeTexDesc();

        tex0Left = RenderTexture.GetTemporary(desc);
        tex1Left = RenderTexture.GetTemporary(desc);
        tex0Right = RenderTexture.GetTemporary(desc);
        tex1Right = RenderTexture.GetTemporary(desc);

        InitCamera(auxCam0Right, 0, tex0Right, StereoTargetEyeMask.Right);
        InitCamera(auxCam0Left, 0, tex0Left, StereoTargetEyeMask.Left);
        InitCamera(auxCam1Left, 1, tex1Left, StereoTargetEyeMask.Left);
        InitCamera(auxCam1Right, 1, tex1Right, StereoTargetEyeMask.Right);
    }

    void PredictCamRigTransform()
    {
        // Update the position of aux rigs
        // Rig 1 represents the position in the past
        // Rig 0 represents the position in the future
        // Put rig 1 to rig 0's old position
        if (timer == 0f)
        {
            // First started locomotion
            auxRig1.transform.SetPositionAndRotation(xrRig.transform.position, xrRig.transform.rotation);
            ampCam1Bias = new Vector3(0, 0, 0);
            ampCam0Bias = new Vector3(0, 0, 0);
            ampCam1RotBias = Quaternion.identity;
            ampCam0RotBias = Quaternion.identity;
        }
        else
        {
            auxRig1.transform.SetPositionAndRotation(auxRig0.transform.position, auxRig0.transform.rotation);
        }

        // Project camera's forward and left vector to the xz plane
        Vector3 xzFwd = Vector3.ProjectOnPlane(mainCam.transform.forward, Vector3.up).normalized;
        Vector3 xzRight = Vector3.ProjectOnPlane(mainCam.transform.right, Vector3.up).normalized;

        // Put rig 0 to the predicted position
        // Predict translation and rotation
        // Translation
        float isTransForward = 0f;
        if (xrRig.position != prevPos)
        {
            Vector3 deltaRigPos = xrRig.position - prevPos;
            if (deltaRigPos.magnitude > minTransPerFrame)
            {
                deltaRigPos = deltaRigPos.normalized;
                // I guess the forward translation direcion should be camera's forward instead
                isTransForward = Mathf.Sign(Vector3.Dot(deltaRigPos, xzFwd)); // positive if translate forward
            }
        }
        // Rotation
        float isRotationRight = RotateDirection();
        Vector3 predictedPos;
        float predictedRotation;
        // If the xrRig is not translating
        if (isTransForward == 0f)
        {
            predictedPos = predicts[0];
            // No rotation too
            if (isRotationRight == 0f)
            {
                predictedRotation = 0f;
                //Debug.Log("NT, NR");
            }
            // Rotate in place
            else
            {
                predictedRotation = isRotationRight * turnSpeed * teleportInterval;
                //fovRestrictorSize = turnFovRestrictorSize;
                //fovOuterSize = turnFovOuterSize;
                //Debug.Log("NT, R");
            }
        }
        else
        {
            // Translate Only
            if (isRotationRight == 0f)
            {
                predictedPos = predicts[2].z * isTransForward * xzFwd;
                predictedRotation = 0f;
                //Debug.Log("T, NR");
            }
            // Translate + Rotate
            else
            {
                // I blocked this condition. I will only allow turning in place
                //predictedPos = predicts[3].z * isTransForward * xzFwd + predicts[3].y * isRotationRight * xzRight;
                predictedPos = predicts[0];
                predictedRotation = isRotationRight * turnSpeed * teleportInterval;
                //Debug.Log("T, R");
            }
            //fovRestrictorSize = turnFovRestrictorSize;
            //fovOuterSize = turnFovOuterSize;
        }
        ampCam1Bias = ampCam0Bias;
        ampCam0Bias = new Vector3(0, 0, 0);
        ampCam1RotBias = ampCam0RotBias;
        ampCam0RotBias = Quaternion.identity;
        auxRig0.transform.position = xrRig.position + predictedPos;
        auxRig0.transform.rotation = xrRig.rotation;
        auxCam0Left.transform.localPosition = mainCam.transform.localPosition;
        auxCam0Left.transform.localRotation = mainCam.transform.localRotation;
        // TESTING: not sure if this is right
        //float xrRigAngle = Quaternion.Angle(xrRig.rotation, auxRig0.transform.rotation);
        auxRig0.transform.RotateAround(auxCam0Left.transform.position, Vector3.up, predictedRotation/* - xrRigAngle*/);
        // Timer repetition
        timer = Mathf.Repeat(timer, teleportInterval);
    }

    protected override void TSUpdateForMovingCamera()
    {
        // Start the timer;
        if (isMoving == false)
        {
            timer = 0;
            isMoving = true;
            // Unrelated: when motion starts, update x scale dynamically according to the editor value
            computeShaderPostProcessing.SetFloat(RstrXScale, fovRestrictorXScale - 1f);
        }
        
        if (timer > teleportInterval || timer == 0f)
        {
            PredictCamRigTransform();
        }
        if (!pausePT) {
            timer += Time.deltaTime;
        }
        // Update the lerp factor in the shader
        if (showPT0)
        {
            computeShaderPostProcessing.SetFloat(PTLerp, 0);
        }
        else if (showPT1)
        {
            computeShaderPostProcessing.SetFloat(PTLerp, 1);
        } else
        {
            computeShaderPostProcessing.SetFloat(PTLerp, timer / teleportInterval);
        }
    }

    protected override void TSLateUpdate()
    {
        UpdateAuxCams();
        // Logging postural sway data
        PosturalSwayUpdate(prevCamPos, mainCam.transform.localPosition);
        // Every render loop, set isRenderLeft to true
        isRenderingLeft = true;
    }

    void PosturalSwayUpdate(Vector3 prevPos, Vector3 currPos)
    {
        Vector3 deltaPos = (currPos - prevPos) / Time.deltaTime;
        if (isPosStationary(prevDeltaCamLocalPos, deltaPos))
        {
            // do something if the postural sway reached a stop point
            //Debug.Log("Sway Stop: " + Time.frameCount);
        }
        prevDeltaCamLocalPos = deltaPos;
    }

    bool isPosStationary(Vector3 prev, Vector3 curr)
    {
        return isXStationary(prev.x, curr.x)
            && isXStationary(prev.y, curr.y)
            && isXStationary(prev.z, curr.z);
    }

    bool isXStationary(float prevDeltaX, float currDeltaX)
    {
        return Mathf.Abs(prevDeltaX) < swayThreshold
            || Mathf.Abs(currDeltaX) < swayThreshold
            || Mathf.Sign(prevDeltaX) != Mathf.Sign(currDeltaX);
    }

    void UpdateAuxCams()
    {
        float halfIPD = mainCam.stereoSeparation * .5f;
        //float rotationLerpFactor = 0f;
        float scaleFactor;

        // Amplify postural sway
        Vector3 deltaCamPos = (mainCam.transform.localPosition - prevCamPos) * (periScale - 1f);
        ampCam0Bias += deltaCamPos;
        ampCam1Bias += deltaCamPos;
        scaleFactor = periScale;
        UpdateCamTransform(auxCam0Left, mainCam.transform.localPosition + ampCam0Bias, mainCam.transform.localRotation * ampCam0RotBias, -halfIPD * scaleFactor);
        UpdateCamTransform(auxCam0Right, mainCam.transform.localPosition + ampCam0Bias, mainCam.transform.localRotation * ampCam0RotBias, halfIPD * scaleFactor);
        UpdateCamTransform(auxCam1Left, mainCam.transform.localPosition + ampCam1Bias, mainCam.transform.localRotation * ampCam1RotBias, -halfIPD * scaleFactor);
        UpdateCamTransform(auxCam1Right, mainCam.transform.localPosition + ampCam1Bias, mainCam.transform.localRotation * ampCam1RotBias, halfIPD * scaleFactor);
    }

    void UpdateCamTransform(Camera c, Vector3 amplifiedSway, Quaternion amplifiedRotation, float halfIPD)
    {
        c.transform.SetLocalPositionAndRotation(amplifiedSway, amplifiedRotation);
        c.transform.position += halfIPD * c.transform.right;
        //Debug.Log("Jitter: " + c.useJitteredProjectionMatrixForTransparentRendering);
    }

    protected override void TSOnRenderImageBeforeBlit()
    {
        // Feed different render textures to the shader for stereo rendering
        if (isRenderingLeft)
        {
            computeShaderPostProcessing.SetTexture(csKernel, PTPTCam0, tex0Left);
            computeShaderPostProcessing.SetTexture(csKernel, PTPTCam1, tex1Left);
        }
        else
        {
            computeShaderPostProcessing.SetTexture(csKernel, PTPTCam0, tex0Right);
            computeShaderPostProcessing.SetTexture(csKernel, PTPTCam1, tex1Right);
        }
        computeShaderPostProcessing.SetInt(csEyeIndex, isRenderingLeft ? 0 : 1);
        isRenderingLeft = !isRenderingLeft;

        // Shader doing its job
        computeShaderPostProcessing.Dispatch(csKernel, dispatchWidth, dispatchHeight, dispatchEye);
    }

    protected override void TSFirstXREnabled()
    {
        // Refactored
        // Render texture settings
        InitRenderTextures();

        // Note: originally, auxCam1Right's projection matrix is wrong. I noticed its m02 is negative while the mainCam's is positive.
        // So I just copied mainCam's projection matrix.
        auxCam1Right.projectionMatrix = mainCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
        auxCam0Right.projectionMatrix = mainCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
    }

    protected override void TSAfterFirstFrame()
    {
    }

    protected override void TSOnRenderImageAfterBlit()
    {
    }

    public void SetDitherSpace(bool ds)
    {
        ditherSpace = ds;
        computeShaderPostProcessing.SetBool(PTDitherSpace, ditherSpace);
    }

    public void SetDitherTime(bool dt)
    {
        ditherTime = dt;
        computeShaderPostProcessing.SetBool(PTDitherTime, ditherTime);
    }
}


