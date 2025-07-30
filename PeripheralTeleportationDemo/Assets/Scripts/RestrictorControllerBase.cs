using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public abstract class RestrictorControllerBase : MonoBehaviour
{
    // General Parameters for a Restrictor
    [Header("Main camera")]
    public Transform xrRig;
    public Camera mainCam;
    [Header("FoV parameters")]
    [Tooltip("Measured in tan(theta/2)")]
    public float fovRestrictorSize = .577f; // tan 30 degs
    public float fovOuterSize = .637f; // tan 32.5 degs
    public float fovMax = 1.4f;
    // translation parameters
    [Header("The only pair of fov restrictor sizes that are used")]
    public float transFovRestrictorSize = .577f;
    public float transturnFovOuterSize = .637f;
    // a different set of parameters for turning
    public float fovRestrictorShift = 0f;
    public float fovRestrictorXScale = 1f;
    public float turnFovRestrictorSize = .45f;
    public float turnFovOuterSize = .5f;
    public float transSqlrPow = 2.0f;
    public float turnSqlrPow = 2.0f;
    public float transitionInterval = .01f;
    public float shiftInterval = .25f;
    public bool forceTurnOn = false;
    public bool forceTurnOff = false;
    public bool forceWholeScreen = false;
    public bool forceBlackPeriphery = false;
    [Tooltip("Minimum translation speed for position prediction")]
    public float minTransThreshold = 1f;
    [Tooltip("Miniumn rotation speed for rotation prediction")]
    public float minRotThreshold = 1f;
    [Header("Post Processing Shader")]
    public ComputeShader computeShaderPostProcessing; // PeripheralTeleportation.compute

    // Shader ids
    protected int RstrRadius,
        RstrXScale, 
        RstrShift,
        RstrTransition, 
        RstrEyeProjection, 
        RstrSqlrPow,
        RstrMovingCam,
        ReverseY,
        RstrForceBlack,
        csKernel,
        csResult;
    protected Matrix4x4[] _eyeProjection = new Matrix4x4[2];
    protected Matrix4x4[] _eyeProjectionNiv = new Matrix4x4[2];
    protected const float kernelGroupSize = 8.0f;
    protected int dispatchWidth = 0;
    protected int dispatchHeight = 0;
    protected int dispatchEye = 2;
    // This timer is for FoV restrictor size and shift only
    protected float fovTimer = 0;
    protected float fovShiftTimer = 0;
    protected float fovLerp = 1; // Transit the fov size between fovRestrictorSize and fovOuterSize
    protected float fovShiftLerp = 0;
    protected bool isMoving = false;
    protected bool isTurning = false;
    protected bool projMatrixEmpty = true;
    protected Vector3 prevPos = new Vector3(0, 0, 0);
    protected Vector3 prevCamPos = new Vector3(0, 0, 0);
    protected Vector3 prevRot = new Vector3(0, 0, 0);
    protected Vector3 prevFwd = new Vector3(0, 0, 0);
    protected Quaternion prevCamRot = Quaternion.identity;
    protected float minTransPerFrame, minRotatePerFrame;
    protected RenderTexture _rTexture;

    protected void InitRestrictorController()
    {
        RstrRadius = Shader.PropertyToID("_RestrictorRadius");
        RstrShift = Shader.PropertyToID("_RestrictorShift");
        RstrXScale = Shader.PropertyToID("_RestrictorXScale");
        RstrTransition = Shader.PropertyToID("_RestrictorTranstion");
        RstrEyeProjection = Shader.PropertyToID("_EyeProjection");
        RstrSqlrPow = Shader.PropertyToID("_SqlrPow");
        RstrMovingCam = Shader.PropertyToID("MovingCam");
        ReverseY = Shader.PropertyToID("_ReverseY");
        RstrForceBlack = Shader.PropertyToID("_ForceBlackPeriphery");

        computeShaderPostProcessing.SetFloat(RstrTransition, fovOuterSize - fovRestrictorSize);
        computeShaderPostProcessing.SetFloat(RstrRadius, fovMax);
        computeShaderPostProcessing.SetFloat(RstrXScale, fovRestrictorXScale - 1f);
        computeShaderPostProcessing.SetFloat(RstrSqlrPow, transSqlrPow);
        // Platform specific coordinate
#if UNITY_EDITOR
        computeShaderPostProcessing.SetFloat(ReverseY, 0);
#else
        computeShaderPostProcessing.SetFloat(ReverseY, 1);
#endif
        computeShaderPostProcessing.SetBool(RstrForceBlack, false);

        csKernel = computeShaderPostProcessing.FindKernel("CSMain");
        csResult = Shader.PropertyToID("Result");

        // Thresholds for prediction
        float fps = XRDevice.refreshRate;
        fps = fps > 0 ? fps : 90f;
        minRotatePerFrame = minRotThreshold * Mathf.PI / (180f * fps);
        fovLerp = fovMax;
    }
    protected void SetTransRestrictorParams()
    {
        fovRestrictorSize = transFovRestrictorSize;
        fovOuterSize = transturnFovOuterSize;
        computeShaderPostProcessing.SetFloat(RstrTransition, fovOuterSize - fovRestrictorSize);
        computeShaderPostProcessing.SetFloat(RstrSqlrPow, transSqlrPow);
        isTurning = false;
    }

    protected void SetTurnRestrictorParams()
    {
        fovRestrictorSize = turnFovRestrictorSize;
        fovOuterSize = turnFovOuterSize;
        computeShaderPostProcessing.SetFloat(RstrTransition, fovOuterSize - fovRestrictorSize);
        computeShaderPostProcessing.SetFloat(RstrSqlrPow, turnSqlrPow);
        isTurning = true;
    }

    protected void SetGameViewMode()
    {
        if (XRSettings.gameViewRenderMode != GameViewRenderMode.BothEyes)
            XRSettings.gameViewRenderMode = GameViewRenderMode.BothEyes;
    }

    protected RenderTextureDescriptor GetXREyeTexDesc()
    {
        RenderTextureDescriptor desc = XRSettings.eyeTextureDesc;
        // XRSettings.eyeTextureDesc is using srgb while src is using linear color space, so we have to set color space manually
        desc.sRGB = false;
        return desc;
    }

    // Return 1 if rotating right, -1 if left
    protected float RotateDirection(/*Vector3 xzRight*/)
    {
        float isRotationRight = 0f;
        if (xrRig.eulerAngles != prevRot)
        {
            Vector3 deltaFwd = xrRig.forward - prevFwd;
            if (deltaFwd.magnitude > minRotatePerFrame)
            {
                //deltaFwd = deltaFwd.normalized;
                Vector3 cross = Vector3.Cross(prevFwd, xrRig.forward);
                isRotationRight = Mathf.Sign(cross.y);
            }
        }
        return isRotationRight;
    }

    protected void UpdateRestrictor()
    {
        float forceWholeScreenFactor = forceWholeScreen ? 0 : 1f;

        // If the xrRig is stationary
        if (xrRig.position == prevPos && xrRig.eulerAngles == prevRot && !forceTurnOn)
        {
            isMoving = false;

            // Stop showing the restrictor
            if (fovLerp <= fovMax)
            {
                fovLerp = Mathf.Lerp(fovRestrictorSize, fovMax, fovTimer / transitionInterval);
                if (fovLerp >= fovMax)
                {
                    fovLerp = fovMax + .0001f;
                    fovTimer = 0;
                }
                fovTimer += Time.deltaTime;
            }
            else
            {
                fovTimer = 0;
            }
            computeShaderPostProcessing.SetFloat(RstrRadius, fovLerp * forceWholeScreenFactor);
            computeShaderPostProcessing.SetFloat(RstrShift, 0);
            // dirty fix
            fovShiftLerp = 0;
        }
        // If the xrRig is not stationary
        else if (!forceTurnOff)
        {
            // Show the restrictor when locomotion starts
            if (fovLerp >= fovRestrictorSize)
            {
                fovLerp = Mathf.Lerp(fovMax, fovRestrictorSize, fovTimer / transitionInterval);
                // No longer requiring the shift to be handled here.
                // See smooth shifting below
                //fovShiftLerp = Mathf.Lerp(0, fovRestrictorShift, fovTimer / transitionInterval);
                if (fovLerp <= fovRestrictorSize)
                {
                    fovLerp = fovRestrictorSize - .0001f;
                    fovTimer = 0;
                }
                fovTimer += Time.deltaTime;
            }
            else
            {
                fovTimer = 0;
            }
            computeShaderPostProcessing.SetFloat(RstrRadius, fovLerp * forceWholeScreenFactor);

            float rotDir = RotateDirection();

            //TODO::smooth shifting
            if (rotDir != 0.0)
            {
                if (Mathf.Abs(fovShiftLerp) <= fovRestrictorShift)
                {
                    //fovShiftLerp = Mathf.Lerp(0, fovRestrictorShift, fovShiftTimer / shiftInterval);
                    fovShiftLerp += rotDir * Time.deltaTime * fovRestrictorShift / shiftInterval;
                    if (Mathf.Abs(fovShiftLerp) >= fovRestrictorShift)
                    {
                        fovShiftLerp = (fovRestrictorShift + 0.0001f) * rotDir;
                        //fovShiftTimer = 0;
                    }
                    //fovShiftTimer += Time.deltaTime;
                }
            }
            else
            {
                if (Mathf.Abs(fovShiftLerp) > 0)
                {
                    float prevFovShiftLerp = fovShiftLerp;
                    fovShiftLerp -= Mathf.Sign(fovShiftLerp) * Time.deltaTime * fovRestrictorShift / shiftInterval;
                    if (prevFovShiftLerp * fovShiftLerp <= 0)
                    {
                        fovShiftLerp = 0;
                    }
                    
                }
            }

            computeShaderPostProcessing.SetFloat(RstrShift, /*rotDir **/ fovShiftLerp);
            if (rotDir != 0)
            {
                SetTurnRestrictorParams();
            }
            else
            {
                SetTransRestrictorParams();
            }
            TSUpdateForMovingCamera();
        }
    }

    protected void UpdateCamAndRig()
    {
        // Update previous position and rotation at the end
        prevRot = xrRig.eulerAngles;
        prevPos = xrRig.position;
        prevCamPos = mainCam.transform.localPosition;
        prevCamRot = mainCam.transform.localRotation;
        prevFwd = xrRig.forward;
    }

    // Technique specific code are marked as abstract functions and abbr as TS
    // Technique specific updates when start
    protected abstract void TSStart();
    // Technique specific updates when the camera is moving for the restrictor are put here
    protected abstract void TSUpdateForMovingCamera();
    // Technique specific updates for LateUpdate
    protected abstract void TSLateUpdate();
    // Technique specific OnRenderImage, before blit happens
    protected abstract void TSOnRenderImageBeforeBlit();
    // Technique specific OnRenderImage, after blit happens
    protected abstract void TSOnRenderImageAfterBlit();
    // Technique specific updates for LateUpdate if XR is enabled
    protected abstract void TSFirstXREnabled();
    // Technique specific updated after first frame
    protected abstract void TSAfterFirstFrame();

    // Start is called before the first frame update
    void Start()
    {
        // Basic functions
        InitRestrictorController();
        // Tech spec
        TSStart();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    protected void LateUpdate()
    {
        // Common procedule
        //SetGameViewMode();
        UpdateRestrictor();


        // Specific
        TSLateUpdate();

        // Common
        UpdateCamAndRig();
    }

    protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (XRSettings.enabled && XRSettings.isDeviceActive)
        {
            RenderTextureDescriptor desc = GetXREyeTexDesc();
            //desc = XRSettings.eyeTextureDesc;
            SetGameViewMode();
            _rTexture = RenderTexture.GetTemporary(desc);
            _rTexture.enableRandomWrite = true;
            computeShaderPostProcessing.SetTexture(csKernel, csResult, _rTexture);
            computeShaderPostProcessing.SetTexture(csKernel, RstrMovingCam, source);

            // Tech specific
            TSOnRenderImageBeforeBlit();

            Graphics.Blit(_rTexture, destination);
            RenderTexture.ReleaseTemporary(_rTexture);

            // Tech specific
            TSOnRenderImageAfterBlit();
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }

    protected void UpdateProjectionMatrices()
    {
        // Copied from TunnellingBase.cs
        //mainCam.CopyStereoDeviceProjectionMatrixToNonJittered(Camera.StereoscopicEye.Left);
        //_eyeProjectionNiv[0] = mainCam.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Left); // tranfer camera view space to camera ndc space
        //mainCam.CopyStereoDeviceProjectionMatrixToNonJittered(Camera.StereoscopicEye.Right);
        //_eyeProjectionNiv[1] = mainCam.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Right);
        //_eyeProjectionNiv[0] = GL.GetGPUProjectionMatrix(_eyeProjectionNiv[0], true);
        //_eyeProjectionNiv[1] = GL.GetGPUProjectionMatrix(_eyeProjectionNiv[1], true);

        //_eyeProjection[0] = _eyeProjectionNiv[0].inverse;
        //_eyeProjection[1] = _eyeProjectionNiv[1].inverse;


        // projection matrices back to work
        // Copied from TunnellingBase.cs
        _eyeProjection[0] = mainCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left); // tranfer camera view space to camera ndc space
        _eyeProjection[1] = mainCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
        _eyeProjection[0] = GL.GetGPUProjectionMatrix(_eyeProjection[0], true).inverse;
        _eyeProjection[1] = GL.GetGPUProjectionMatrix(_eyeProjection[1], true).inverse;

        _eyeProjectionNiv[0] = _eyeProjection[0].inverse;
        _eyeProjectionNiv[1] = _eyeProjection[1].inverse;
#if (!UNITY_STANDALONE_OSX && !UNITY_ANDROID) || UNITY_EDITOR_WIN
        var api = SystemInfo.graphicsDeviceType;
        if (
            api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 &&
            api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2 &&
            api != UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore &&
            api != UnityEngine.Rendering.GraphicsDeviceType.Vulkan
        )
        {
            _eyeProjection[0][1, 1] *= -1f;
            _eyeProjection[1][1, 1] *= -1f;
        }
        _eyeProjectionNiv[0] = _eyeProjection[0].inverse;
        _eyeProjectionNiv[1] = _eyeProjection[1].inverse;
#endif
        computeShaderPostProcessing.SetMatrixArray(RstrEyeProjection, _eyeProjection);

    }

    protected void OnPreRender()
    {
        // Do this for only once
        if (XRSettings.enabled && XRSettings.isDeviceActive && projMatrixEmpty)
        {
            // Refactored
            UpdateProjectionMatrices();
            dispatchWidth= Mathf.CeilToInt(mainCam.pixelWidth / kernelGroupSize);
            dispatchHeight = Mathf.CeilToInt(mainCam.pixelHeight / kernelGroupSize);

            // Refactored, TS
            TSFirstXREnabled();

            projMatrixEmpty = false;
        } else
        {
            TSAfterFirstFrame();
        }
        
    }

    public void SetBlackPeriphery(bool bp)
    {
        forceBlackPeriphery = bp;
        computeShaderPostProcessing.SetBool(RstrForceBlack, forceBlackPeriphery);
    }

    public void ResetPrevTrans()
    {
        UpdateCamAndRig();
        //fovTimer = 0;
        fovLerp = fovMax + 0.001f;
    }
}
