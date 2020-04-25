// Copyright (c) 2017 Jakub Boksansky, Adam Pospisil - All Rights Reserved
// Wilberforce Colorbleed Unity Plugin 1.0

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;


namespace Wilberforce.Colorbleed
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [HelpURL("https://projectwilberforce.github.io/colorbleed/")]
    [AddComponentMenu("Image Effects/Rendering/Colorbleed")]
    public class Colorbleed : MonoBehaviour
    {
       
        public enum LuminanceModeType
        {
            Luma = 1,
            HSVValue = 2,
        }

        public enum GiBlurAmmount
        {
            Auto = 1,
            Less = 2,
            More = 3
        }

        public enum DoomModeType
        {
            Off = 0,
            Greedy = 1,
            Careful = 2
        }

        public enum AdaptiveSamplingType
        {
            Disabled = 0,
            EnabledAutomatic = 1,
            EnabledManual = 2
        }


        public enum ColorbleedAlgorithmType
        {
            PointCloud = 0,
            Raymarching = 1
        }

        // Shader
        public Shader colorbleedShader;

        // Public Parameters
        public float Radius = 0.5f;
        public int Quality = 16;

        public float Saturation = 1.0f;
        public int BlurSize = 5;
        public bool EnhancedBlur = true;
	    public float Deviation = 1.5f;

        public float distanceAttPower = 2.0f;
        public bool distanceAtt = true;
        public bool cosineAtt = true;

        public bool OutputCBOnly = false;
        public bool DoBlur = true;

        public AdaptiveSamplingType AdaptiveType = AdaptiveSamplingType.EnabledAutomatic;
        private float AdaptiveQuality = 0.2f;
        public float AdaptiveQualityCoefficient = 1.0f;

        private float AdaptiveMin = 0.0f;
        private float AdaptiveMax = -10.0f;

        public DoomModeType DoomMode = DoomModeType.Careful;
        private int DoomFactor = 8;

        public bool IsLumaSensitive = false;
        public float LumaTreshold = 0.7f;
        public float LumaKneeLinearity = 3.0f;
        public float LumaKneeWidth = 0.3f;

        private Vector4[] gaussian = null;
        private float gaussianWeight = 0.0f;
        private float lastDeviation = 0.5f;

        public LuminanceModeType LuminanceMode = LuminanceModeType.Luma;

        public float ColorBleedPower = 3.0f;
        public float ColorBleedPresence = 1.0f;
        public float ColorBleedBrightness = 0.6f;
        public bool GiBackfaces = false;
        
        public int RaymarchStepsCount = 4;

        public int Downsampling = 1;

        public ColorbleedAlgorithmType Algorithm = ColorbleedAlgorithmType.PointCloud;

        public bool CommandBufferEnabled = false;
        public bool UseGBuffer = false;
        public bool ForcedSwitchPerformed = false;
        public CameraEvent CameraEvent = CameraEvent.BeforeImageEffectsOpaque;
        private Dictionary<CameraEvent, CommandBuffer> cameraEventsRegistered = new Dictionary<CameraEvent, CommandBuffer>();
        private bool isCommandBufferAlive = false;
        private bool settingsDirty = false;
        private CameraEvent lastCameraEvent;

        public float BackfacesRadiusMultiplier = 0.3f;

        private Camera myCamera = null;

        // Private fields
        private bool isSupported;
        private Material ColorbleedMaterial;

        // To prevent error with capping of large array to smaller size in Unity 5.4 - always use largest array filled with trailing zeros.
#if UNITY_5_4_OR_NEWER
        private Vector4[] samplesLarge = new Vector4[70];
        int lastSamplesLength = 0;
#endif

        private enum ShaderPass
        {
            Colorbleed = 0,
            ColorbleedDoomPass = 1,
            ColorbleedGBuffer = 2,
            ColorbleedDoomPassGBuffer = 3,

            ColorbleedRaymarch = 4,
            ColorbleedRaymarchDoomPass = 5,
            ColorbleedRaymarchGBuffer = 6,
            ColorbleedRaymarchDoomPassGBuffer = 7,

            EnhancedBlurFirst = 8,
            EnhancedBlurSecond = 9,

            Mixing = 10,
            MixingCBOnly = 11
        }

        private void ReportError(string error)
        {
            if (Debug.isDebugBuild) Debug.LogError("Colorbleed Effect Error: " + error);
        }

        private void ReportWarning(string error)
        {
            if (Debug.isDebugBuild) Debug.LogWarning("Colorbleed Effect Warning: " + error);
        }

        private void EnsureCommandBuffer(bool settingsDirty = false)
        {
            if ((!settingsDirty && isCommandBufferAlive) || !CommandBufferEnabled) return;

            try
            {
                CreateCommandBuffer();
                lastCameraEvent = CameraEvent;
                isCommandBufferAlive = true;
            }
            catch (Exception ex)
            {
                ReportError("There was an error while trying to create command buffer. " + ex.Message);
            }
        }

        private void TeardownCommandBuffer()
        {
            if (!isCommandBufferAlive) return;

            try
            {
                isCommandBufferAlive = false;
                
                if (myCamera != null)
                {
                    foreach (var e in cameraEventsRegistered)
                    {
                        myCamera.RemoveCommandBuffer(e.Key, e.Value);
                    }
                }

                cameraEventsRegistered.Clear();
                ColorbleedMaterial = null;
                EnsureMaterials();
            }
            catch (Exception ex)
            {
                ReportError("There was an error while trying to destroy command buffer. " + ex.Message);
            }
        }

        private ShaderPass GetNoisePass(ColorbleedAlgorithmType algorithm, bool isDoomMode, bool useGBuffer) {

            switch (algorithm) {
                case ColorbleedAlgorithmType.PointCloud:
                    if (isDoomMode)
                        return useGBuffer ? ShaderPass.ColorbleedDoomPassGBuffer : ShaderPass.ColorbleedDoomPass;
                    else 
                        return useGBuffer ? ShaderPass.ColorbleedGBuffer : ShaderPass.Colorbleed;
                case ColorbleedAlgorithmType.Raymarching:
                    if (isDoomMode)
                        return useGBuffer ? ShaderPass.ColorbleedRaymarchDoomPassGBuffer : ShaderPass.ColorbleedRaymarchDoomPass;
                    else
                        return useGBuffer ? ShaderPass.ColorbleedRaymarchGBuffer : ShaderPass.ColorbleedRaymarch;
            }

            ReportError("Unknown algorithm " + algorithm.ToString() + " selected for colorbleeding. Reverting to default.");

            if (isDoomMode)
                return ShaderPass.ColorbleedDoomPass;
            else
                return ShaderPass.Colorbleed;
        }


        private ShaderPass GetBlurPass(bool useGBuffer, bool isFirstPass)
        {
            if (isFirstPass)
                return ShaderPass.EnhancedBlurFirst;
            else
                return ShaderPass.EnhancedBlurSecond;
        }

        private ShaderPass GetMixingPass(bool cbOnly)
        {
            if (cbOnly)
                return ShaderPass.MixingCBOnly;
            else
                return ShaderPass.Mixing;
        }

        private void CreateCommandBuffer()
        {
            CommandBuffer commandBuffer;

            ColorbleedMaterial = null;
            EnsureMaterials();

            if (cameraEventsRegistered.TryGetValue(CameraEvent, out commandBuffer))
            {
                commandBuffer.Clear();
            }
            else
            {
                commandBuffer = new CommandBuffer();
                myCamera.AddCommandBuffer(CameraEvent, commandBuffer);

                commandBuffer.name = "Colorbleed";

                // Register
                cameraEventsRegistered[CameraEvent] = commandBuffer;
            }

            int screenTextureWidth = myCamera.pixelWidth / Downsampling;
            int screenTextureHeight = myCamera.pixelHeight / Downsampling;

            int screenTexture = Shader.PropertyToID("screenTextureRT");
            commandBuffer.GetTemporaryRT(screenTexture, screenTextureWidth, screenTextureHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            int colorbleedTexture = Shader.PropertyToID("colorbleedTextureRT");
            commandBuffer.GetTemporaryRT(colorbleedTexture, screenTextureWidth, screenTextureHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            int? doomTexture = null;

            // Remember input
            commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, screenTexture);

            if (DoomMode != DoomModeType.Off)
            {
                doomTexture = Shader.PropertyToID("doomTextureRT");
                commandBuffer.GetTemporaryRT(doomTexture.Value, screenTextureWidth / DoomFactor, screenTextureHeight / DoomFactor, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                commandBuffer.Blit(screenTexture, doomTexture.Value, ColorbleedMaterial, (int) GetNoisePass(Algorithm, true, UseGBuffer));
                commandBuffer.SetGlobalTexture("doomTexture", doomTexture.Value);
            }

            // noise pass
            commandBuffer.Blit(screenTexture, colorbleedTexture, ColorbleedMaterial, (int) GetNoisePass(Algorithm, false, UseGBuffer));

            if (DoBlur)
            {
                int blurTextureWidth = myCamera.pixelWidth;
                int blurTextureHeight = myCamera.pixelHeight;

                // Blur pass
                int blurredGiTexture = Shader.PropertyToID("blurredGiTextureRT");
                commandBuffer.GetTemporaryRT(blurredGiTexture, blurTextureWidth, blurTextureHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                
                int tempTexture = Shader.PropertyToID("tempTextureRT");
                commandBuffer.GetTemporaryRT(tempTexture, blurTextureWidth, blurTextureHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

                commandBuffer.Blit(colorbleedTexture, tempTexture, ColorbleedMaterial, (int)GetBlurPass(UseGBuffer, true));
                commandBuffer.Blit(tempTexture, blurredGiTexture, ColorbleedMaterial, (int)GetBlurPass(UseGBuffer, false));

                commandBuffer.ReleaseTemporaryRT(tempTexture);
               
                // Mixing pass
                commandBuffer.SetGlobalTexture("textureCB", blurredGiTexture);
                commandBuffer.Blit(screenTexture, BuiltinRenderTextureType.CameraTarget, ColorbleedMaterial, (int)GetMixingPass(OutputCBOnly));

                // Cleanup
                commandBuffer.ReleaseTemporaryRT(blurredGiTexture);
                commandBuffer.ReleaseTemporaryRT(colorbleedTexture);
            }
            else
            {
                // Mixing pass
                commandBuffer.SetGlobalTexture("textureCB", colorbleedTexture);
                commandBuffer.Blit(screenTexture, BuiltinRenderTextureType.CameraTarget, ColorbleedMaterial, (int)GetMixingPass(OutputCBOnly));

                commandBuffer.ReleaseTemporaryRT(colorbleedTexture);
            }

            if (doomTexture != null) commandBuffer.ReleaseTemporaryRT(doomTexture.Value);

            // Cleanup
            commandBuffer.ReleaseTemporaryRT(screenTexture);
        }


        void Start()
        {
            if (colorbleedShader == null) colorbleedShader = Shader.Find("Hidden/Wilberforce/Colorbleed");

            if (colorbleedShader == null)
            {
                ReportError("Could not locate Colorbleed Shader. Make sure there is 'Colorbleed.shader' file added to the project.");
                isSupported = false;
                enabled = false;
                return;
            }

            if (!SystemInfo.supportsImageEffects || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth) || SystemInfo.graphicsShaderLevel < 30)
            {
                if (!SystemInfo.supportsImageEffects) ReportError("System does not support image effects.");
                if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth)) ReportError("System does not support depth texture.");
                if (SystemInfo.graphicsShaderLevel < 30) ReportError("This effect needs at least Shader Model 3.0.");

                isSupported = false;
                enabled = false;
                return;
            }

            EnsureMaterials();

            if (!ColorbleedMaterial || ColorbleedMaterial.passCount != 12)
            {
                ReportError("Could not create shader.");
                isSupported = false;
                enabled = false;
                return;
            }

            if (adaptiveSamples == null) adaptiveSamples = GenerateAdaptiveSamples();

            isSupported = true;
        }

        void OnEnable()
        {
            this.myCamera = GetComponent<Camera>();
            TeardownCommandBuffer();

            // See if there is post processing stuck
            if (myCamera != null && (CommandBufferEnabled == false || UseGBuffer == false))
            {
                try
                {
                    System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                    Type postStackType = asm.GetType("UnityEngine.PostProcessing.PostProcessingBehaviour");
                    var postStack = GetComponent(postStackType);
                    if (postStack != null)
                    {
                        if (ForcedSwitchPerformed)
                        {
                            ReportWarning("Post Processing Stack Detected! We recommend switching to command buffer pipeline and GBuffer inputs if you encounter compatibility problems.");
                        }
                        else
                        {
                            ReportWarning("Post Processing Stack Detected! Switching to command buffer pipeline and GBuffer inputs!");
                            CommandBufferEnabled = true;
                            UseGBuffer = true;
                            ForcedSwitchPerformed = true;
                        }
                    }
                }
                catch (Exception) { }
            }
        }

        void OnDisable()
        {
            TeardownCommandBuffer();
        }

        void OnPreRender()
        {
            DepthTextureMode currentMode = myCamera.depthTextureMode;
            if (myCamera.actualRenderingPath == RenderingPath.DeferredShading && UseGBuffer)
            {
                if ((currentMode & DepthTextureMode.Depth) != DepthTextureMode.Depth)
                {
                    myCamera.depthTextureMode |= DepthTextureMode.Depth;
                }
            }
            else
            {
                if ((currentMode & DepthTextureMode.DepthNormals) != DepthTextureMode.DepthNormals)
                {
                    myCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
                }
            }

            CheckSettingsChanges();
            EnsureCommandBuffer(settingsDirty);
            TrySetUniforms();
        }

        private int lastDownsampling;
        private DoomModeType lastDoomMode;
        private int lastDoomFactor;
        private bool lastDoBlur;
        private bool lastEnhancedBlur;
        
        private void CheckSettingsChanges()
        {
            if (CameraEvent != lastCameraEvent)
            {
                TeardownCommandBuffer();
            }

            if (Downsampling != lastDownsampling)
            {
                lastDownsampling = Downsampling;
                settingsDirty = true;
            }

            if (DoomMode != lastDoomMode)
            {
                lastDoomMode = DoomMode;
                settingsDirty = true;
            }

            if (DoomFactor != lastDoomFactor)
            {
                lastDoomFactor = DoomFactor;
                settingsDirty = true;
            }

            if (DoBlur != lastDoBlur)
            {
                lastDoBlur = DoBlur;
                settingsDirty = true;
            }

            if (EnhancedBlur != lastEnhancedBlur)
            {
                lastEnhancedBlur = EnhancedBlur;
                settingsDirty = true;
            }
        }

        private void TrySetUniforms()
        {
            if (ColorbleedMaterial == null) return;

            int screenTextureWidth = myCamera.pixelWidth / Downsampling;
            int screenTextureHeight = myCamera.pixelHeight / Downsampling;

            Vector4[] samples = null;
            float sampSize = 0.0f;
            switch (Quality)
            {
                case 2:
                    samples = samp2;
                    sampSize = sampSize2;
                    break;
                case 4:
                    samples = samp4;
                    sampSize = sampSize4;
                    break;
                case 8:
                    samples = samp8;
                    sampSize = sampSize8;
                    break;
                case 16:
                    samples = samp16;
                    sampSize = sampSize16;
                    break;
                case 32:
                    samples = samp32;
                    sampSize = sampSize32;
                    break;
                case 64:
                    samples = samp64;
                    sampSize = sampSize64;
                    break;
                default:
                    ReportError("Unsupported quality setting " + Quality + " encountered. Reverting to low setting");
                    // Reverting to low
                    Quality = 16;
                    samples = samp16;
                    sampSize = sampSize16;
                    break;
            }

            if (AdaptiveType != AdaptiveSamplingType.Disabled)
            {
                switch (Quality)
                {
                    case 64: AdaptiveQuality = 0.025f; break;
                    case 32: AdaptiveQuality = 0.025f; break;
                    case 16: AdaptiveQuality = 0.05f; break;
                    case 8: AdaptiveQuality = 0.1f; break;
                    case 4: AdaptiveQuality = 0.2f; break;
                    case 2: AdaptiveQuality = 0.4f; break;
                }
                if (AdaptiveType == AdaptiveSamplingType.EnabledManual)
                {
                    AdaptiveQuality *= AdaptiveQualityCoefficient;
                }
                else
                {
                    AdaptiveQualityCoefficient = 1.0f;
                }
            }

            MyFunction(myCamera);

            // Set shader uniforms
            ColorbleedMaterial.SetMatrix("projMatrix", myCamera.projectionMatrix);
            ColorbleedMaterial.SetMatrix("invProjMatrix", myCamera.projectionMatrix.inverse);
            ColorbleedMaterial.SetFloat("_half", Radius * 0.5f);
            ColorbleedMaterial.SetFloat("radius", Radius);
            ColorbleedMaterial.SetFloat("_fullRcp", 1.0f / Radius);
            ColorbleedMaterial.SetInt("_length", Quality);
            ColorbleedMaterial.SetInt("_lengthOpenGLBug", Quality);
            ColorbleedMaterial.SetFloat("bigweight", 1.0f / (sampSize * Radius));
            ColorbleedMaterial.SetInt("lumaSensitivityOn", IsLumaSensitive ? 1 : 0);
            ColorbleedMaterial.SetFloat("LumaTreshold", LumaTreshold);
            ColorbleedMaterial.SetFloat("LumaKneeWidth", LumaKneeWidth);
            ColorbleedMaterial.SetFloat("LumaTwiceKneeWidthRcp", 1.0f / (LumaKneeWidth * 2.0f));
            ColorbleedMaterial.SetFloat("LumaKneeLinearity", LumaKneeLinearity);
            ColorbleedMaterial.SetInt("isCBOnly", OutputCBOnly ? 1 : 0);
            ColorbleedMaterial.SetInt("lumaMode", (int)LuminanceMode);
            ColorbleedMaterial.SetInt("giBackfaces", GiBackfaces ? 1 : 0);
            ColorbleedMaterial.SetInt("doomMode", (int) DoomMode);
            ColorbleedMaterial.SetInt("adaptive", (int) AdaptiveType);
            ColorbleedMaterial.SetFloat("adaptiveMin", AdaptiveMin);
            ColorbleedMaterial.SetFloat("adaptiveMax", AdaptiveMax);
            ColorbleedMaterial.SetFloat("distanceAttPower", distanceAttPower);
            ColorbleedMaterial.SetInt("distanceAtt", distanceAtt ? 1 : 0);
            ColorbleedMaterial.SetInt("cosineAtt", cosineAtt ? 1 : 0);
            ColorbleedMaterial.SetInt("useGBuffer", UseGBuffer ? 1 : 0);
            ColorbleedMaterial.SetFloat("giPow", ColorBleedPower);
            ColorbleedMaterial.SetFloat("_giPresence", 1.0f - ColorBleedPresence);
            ColorbleedMaterial.SetFloat("giDarkness", ColorBleedBrightness);
            ColorbleedMaterial.SetVector("_tsize", new Vector2(1.0f / screenTextureWidth, 1.0f / screenTextureHeight));
            ColorbleedMaterial.SetFloat("_tresh", Radius);
            ColorbleedMaterial.SetVector("_tsizedoom", new Vector2(0.5f / (myCamera.pixelWidth / DoomFactor), 0.5f / (myCamera.pixelHeight / DoomFactor)));
            ColorbleedMaterial.SetFloat("bigweightEight", 1.0f / (sampSize8 * Radius));
            ColorbleedMaterial.SetInt("forceFlip", MustForceFlip(myCamera) ? 1 : 0);
            ColorbleedMaterial.SetInt("enhancedBlurSize", BlurSize / 2);
            ColorbleedMaterial.SetInt("adaptiveMode", (int)AdaptiveType);
            ColorbleedMaterial.SetInt("IsLumaSensitive", IsLumaSensitive ? 1 : 0);
            ColorbleedMaterial.SetInt("LumaMode", (int)LuminanceMode);
            ColorbleedMaterial.SetInt("_raymarchSteps", (int)RaymarchStepsCount);
            ColorbleedMaterial.SetFloat("backfacesRadiusMultiplier", GiBackfaces ? 1.0f-BackfacesRadiusMultiplier : 1.0f);
            ColorbleedMaterial.SetFloat("_saturation", 1.0f - Saturation);

            if (DoBlur)
            {
                if (gaussian == null || gaussian.Length != BlurSize || Deviation != lastDeviation)
                {
                    gaussian = GenerateGaussian(BlurSize, Deviation, out gaussianWeight);
                    lastDeviation = Deviation;
                }

                ColorbleedMaterial.SetFloat("gaussWeight", gaussianWeight);

#if UNITY_5_4_OR_NEWER
                ColorbleedMaterial.SetVectorArray("gauss", gaussian);
#else
                for (int i = 0; i < gaussian.Length; ++i)
                {
                    ColorbleedMaterial.SetVector("gauss" + i.ToString(), gaussian[i]);
                }
#endif
            }

            if (DoomMode != DoomModeType.Off)
            {
                SetSampleSetNoBuffer("samp8", ColorbleedMaterial, samp8);
            }

            if (AdaptiveType != 0)
            {
                SetSampleSet("_samp", ColorbleedMaterial, GetAdaptiveSamples());
            }
            else
            {
                if (DoomMode == DoomModeType.Careful)
                {
                    SetSampleSet("_samp", ColorbleedMaterial, GetCarefulDoomSamples(samples, samp4));
                }
                else
                {
                    SetSampleSet("_samp", ColorbleedMaterial, samples);
                }
            }

        }

        private static Material CreateMaterial(Shader shader)
        {
            if (!shader) return null;

            Material m = new Material(shader);
            m.hideFlags = HideFlags.HideAndDontSave;

            return m;
        }

        private static void DestroyMaterial(Material mat)
        {
            if (mat)
            {
                DestroyImmediate(mat);
                mat = null;
            }
        }

        private void EnsureMaterials()
        {
            if (!ColorbleedMaterial && colorbleedShader.isSupported)
            {
                ColorbleedMaterial = CreateMaterial(colorbleedShader);
            }

            if (!colorbleedShader.isSupported)
            {
                ReportError("Could not create shader (Shader not supported).");
            }
        }

        [ImageEffectOpaque]
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!isSupported || !colorbleedShader.isSupported)
            {
                enabled = false;
                return;
            }

            EnsureMaterials();

            if (CommandBufferEnabled)
            {
                Graphics.Blit(source, destination);
                return; //< Return here, drawing will be done in command buffer
            }
            else
            {
                TeardownCommandBuffer();
            }

            int screenTextureWidth = source.width / Downsampling;
            int screenTextureHeight = source.height / Downsampling;

            // Create temporary texture for GI
            RenderTexture colorbleedTexture = RenderTexture.GetTemporary(screenTextureWidth, screenTextureHeight, 0);

            // Doom pass
            RenderTexture doomTexture = null;
            if (DoomMode != 0)
            {
                doomTexture = RenderTexture.GetTemporary(source.width / DoomFactor, source.height / DoomFactor, 0);
                Graphics.Blit(source, doomTexture, ColorbleedMaterial, (int)GetNoisePass(Algorithm, true, UseGBuffer));
                //Graphics.Blit(doomTexture, destination);
                //return;
            }
           
            // noise pass
            if (doomTexture != null) ColorbleedMaterial.SetTexture("doomTexture", doomTexture);
            Graphics.Blit(source, colorbleedTexture, ColorbleedMaterial, (int) GetNoisePass(Algorithm, false, UseGBuffer));
     
            if (DoBlur)
            {
                int blurTextureWidth = source.width;
                int blurTextureHeight = source.height;

                // Blur pass
                RenderTexture blurredCBTexture = RenderTexture.GetTemporary(blurTextureWidth, blurTextureHeight, 0);
                RenderTexture tempTexture = RenderTexture.GetTemporary(blurTextureWidth, blurTextureHeight, 0);

                Graphics.Blit(colorbleedTexture, tempTexture, ColorbleedMaterial, (int)GetBlurPass(UseGBuffer, true));
                Graphics.Blit(tempTexture, blurredCBTexture, ColorbleedMaterial, (int)GetBlurPass(UseGBuffer, false));

                RenderTexture.ReleaseTemporary(tempTexture);

                // Mixing pass
                ColorbleedMaterial.SetTexture("textureCB", blurredCBTexture);
                Graphics.Blit(source, destination, ColorbleedMaterial, (int)GetMixingPass(OutputCBOnly));
                
                // Cleanup
                RenderTexture.ReleaseTemporary(blurredCBTexture);
                RenderTexture.ReleaseTemporary(colorbleedTexture);
            }
            else
            {
                // Mixing pass
                ColorbleedMaterial.SetTexture("textureCB", colorbleedTexture);
                Graphics.Blit(source, destination, ColorbleedMaterial, (int)GetMixingPass(OutputCBOnly));

                RenderTexture.ReleaseTemporary(colorbleedTexture);
            }

            if (doomTexture != null) RenderTexture.ReleaseTemporary(doomTexture);

        }

        private Vector4[] adaptiveSamples = null;
        private Vector4[] GetAdaptiveSamples()
        {
            if (adaptiveSamples == null) adaptiveSamples = GenerateAdaptiveSamples();
            return adaptiveSamples;
        }

        private Vector4[] carefulCache = null;
        private Vector4[] GetCarefulDoomSamples(Vector4[] samples, Vector4[] carefulSamples)
        {
            if (carefulCache != null && carefulCache.Length == (samples.Length + carefulSamples.Length)) return carefulCache;

            carefulCache = new Vector4[samples.Length + carefulSamples.Length];

            Array.Copy(samples, 0, carefulCache, 0, samples.Length);
            Array.Copy(carefulSamples, 0, carefulCache, samples.Length, carefulSamples.Length);

            return carefulCache;
        }

        private Vector4[] GenerateAdaptiveSamples()
        {
            Vector4[] result = new Vector4[62];

            Array.Copy(samp32, 0, result, 0, 32);
            Array.Copy(samp16, 0, result, 32, 16);
            Array.Copy(samp8, 0, result, 48, 8);
            Array.Copy(samp4, 0, result, 56, 4);
            Array.Copy(samp2, 0, result, 60, 2);

            return result;
        }

        /// <summary>
        /// Description here
        /// </summary>
        /// <param name="camera">Der Geraet</param>
        private void MyFunction(Camera camera)
        {
            AdaptiveMax = -(Radius * camera.projectionMatrix.m11) / AdaptiveQuality;
        }

        private bool MustForceFlip(Camera camera)
        {
#if UNITY_5_6_OR_NEWER
            return false;
#else
            if (myCamera.stereoEnabled)
            {
                return false;
            }
            if (!CommandBufferEnabled) return false;
            if (camera.actualRenderingPath != RenderingPath.DeferredShading && camera.actualRenderingPath != RenderingPath.DeferredLighting) return true;
            return false;
#endif
        }

        
        private Vector4[] GenerateGaussian(int size, float d, out float gaussianWeight)
        {
            Vector4[] result = new Vector4[50];
            float norm = 0.0f;
            for (int i = 0; i <= size / 2; i++)
            {
                // sampling the bell curve
                float temp = (float)((Math.Pow(Math.E, -3.0f * i / (size * d))) / Math.Sqrt(2.0 * d * d * Math.PI));
                // symmetrical...
                result[size / 2 + i].x = temp;
                result[size / 2 - i].x = temp;
                // get total weight for normalization, account for symmetry, but don't count the middle sample twice
                norm += i > 0 ? temp * 2 : temp;
            }
            // normalize
            for (int i = 0; i < size; i++)
            {
                result[i] /= norm;
            }

            gaussianWeight = 0.0f;
            for (int i = 0; i < size; i++)
            {
                gaussianWeight += result[i].x;
            }
            // we're done here - move along
            return result;
        }


        private void SetSampleSetNoBuffer(string name, Material CBMaterial, Vector4[] samples)
        {
#if UNITY_5_4_OR_NEWER
            CBMaterial.SetVectorArray(name, samples);
#else
            for (int i = 0; i < samples.Length; ++i)
            {
                CBMaterial.SetVector(name + i.ToString(), samples[i]);
            }
#endif
        }

        private void SetSampleSet(string name, Material CBMaterial, Vector4[] samples)
        {
#if UNITY_5_4_OR_NEWER

            if (lastSamplesLength != samples.Length)
            {
                Array.Copy(samples, samplesLarge, samples.Length);
                lastSamplesLength = samples.Length;
            }

            CBMaterial.SetVectorArray(name, samplesLarge);
#else
            for (int i = 0; i < samples.Length; ++i)
            {
                CBMaterial.SetVector(name + i.ToString(), samples[i]);
            }
#endif
        }

        #region  Data

        private static Vector4[] samp2 = new Vector4[2] {
        new Vector4(0.4392292f,  0.0127914f, 0.898284f),
        new Vector4(-0.894406f,  -0.162116f, 0.41684f)};
        private static Vector4[] samp4 = new Vector4[4] {
        new Vector4(-0.07984404f,  -0.2016976f, 0.976188f),
        new Vector4(0.4685118f,  -0.8404996f, 0.272135f),
        new Vector4(-0.793633f,  0.293059f, 0.533164f),
        new Vector4(0.2998218f,  0.4641494f, 0.83347f)};
        private static Vector4[] samp8 = new Vector4[8] {
        new Vector4(-0.4999112f,  -0.571184f, 0.651028f),
        new Vector4(0.2267525f,  -0.668142f, 0.708639f),
        new Vector4(0.0657284f,  -0.123769f, 0.990132f),
        new Vector4(0.9259827f,  -0.2030669f, 0.318307f),
        new Vector4(-0.9850165f,  0.1247843f, 0.119042f),
        new Vector4(-0.2988613f,  0.2567392f, 0.919112f),
        new Vector4(0.4734727f,  0.2830991f, 0.834073f),
        new Vector4(0.1319883f,  0.9544416f, 0.267621f)};
        private static Vector4[] samp16 = new Vector4[16] {
        new Vector4(-0.6870962f,  -0.7179669f, 0.111458f),
        new Vector4(-0.2574025f,  -0.6144419f, 0.745791f),
        new Vector4(-0.408366f,  -0.162244f, 0.898284f),
        new Vector4(-0.07098053f,  0.02052395f, 0.997267f),
        new Vector4(0.2019972f,  -0.760972f, 0.616538f),
        new Vector4(0.706282f,  -0.6368136f, 0.309248f),
        new Vector4(0.169605f,  -0.2892981f, 0.942094f),
        new Vector4(0.7644456f,  -0.05826119f, 0.64205f),
        new Vector4(-0.745912f,  0.0501786f, 0.664152f),
        new Vector4(-0.7588732f,  0.4313389f, 0.487911f),
        new Vector4(-0.3806622f,  0.3446409f, 0.85809f),
        new Vector4(-0.1296651f,  0.8794711f, 0.45795f),
        new Vector4(0.1557318f,  0.137468f, 0.978187f),
        new Vector4(0.5990864f,  0.2485375f, 0.761133f),
        new Vector4(0.1727637f,  0.5753375f, 0.799462f),
        new Vector4(0.5883294f,  0.7348878f, 0.337355f)};
        private static Vector4[] samp32 = new Vector4[32] {
        new Vector4(-0.626056f,  -0.7776781f, 0.0571977f),
        new Vector4(-0.1335098f,  -0.9164876f, 0.377127f),
        new Vector4(-0.2668636f,  -0.5663173f, 0.779787f),
        new Vector4(-0.5712572f,  -0.4639561f, 0.67706f),
        new Vector4(-0.6571807f,  -0.2969118f, 0.692789f),
        new Vector4(-0.8896923f,  -0.1314662f, 0.437223f),
        new Vector4(-0.5037534f,  -0.03057539f, 0.863306f),
        new Vector4(-0.1773856f,  -0.2664998f, 0.947371f),
        new Vector4(-0.02786797f,  -0.02453661f, 0.99931f),
        new Vector4(0.173095f,  -0.964425f, 0.199805f),
        new Vector4(0.280491f,  -0.716259f, 0.638982f),
        new Vector4(0.7610048f,  -0.4987299f, 0.414898f),
        new Vector4(0.135136f,  -0.388973f, 0.911284f),
        new Vector4(0.4836829f,  -0.4782286f, 0.73304f),
        new Vector4(0.1905736f,  -0.1039435f, 0.976154f),
        new Vector4(0.4855643f,  0.01388972f, 0.87409f),
        new Vector4(0.5684234f,  -0.2864941f, 0.771243f),
        new Vector4(0.8165832f,  0.01384446f, 0.577062f),
        new Vector4(-0.9814694f,  0.18555f, 0.0478435f),
        new Vector4(-0.5357604f,  0.3316899f, 0.776494f),
        new Vector4(-0.1238877f,  0.03315933f, 0.991742f),
        new Vector4(-0.1610546f,  0.3801286f, 0.910804f),
        new Vector4(-0.5923722f,  0.628729f, 0.503781f),
        new Vector4(-0.05504921f,  0.5483891f, 0.834409f),
        new Vector4(-0.3805041f,  0.8377199f, 0.391717f),
        new Vector4(-0.101651f,  0.9530866f, 0.285119f),
        new Vector4(0.1613653f,  0.2561041f, 0.953085f),
        new Vector4(0.4533991f,  0.2896196f, 0.842941f),
        new Vector4(0.6665574f,  0.4639243f, 0.583503f),
        new Vector4(0.8873722f,  0.4278904f, 0.1717f),
        new Vector4(0.2869751f,  0.732805f, 0.616962f),
        new Vector4(0.4188429f,  0.7185978f, 0.555147f)};
        private static Vector4[] samp64 = new Vector4[64] {
        new Vector4(-0.6700248f,  -0.6370129f, 0.381157f),
        new Vector4(-0.7385408f,  -0.6073685f, 0.292679f),
        new Vector4(-0.4108568f,  -0.8852778f, 0.2179f),
        new Vector4(-0.3058583f,  -0.8047022f, 0.508828f),
        new Vector4(0.01087609f,  -0.7610992f, 0.648545f),
        new Vector4(-0.3629634f,  -0.5480431f, 0.753595f),
        new Vector4(-0.1480379f,  -0.6927805f, 0.70579f),
        new Vector4(-0.9533184f,  -0.276674f, 0.12098f),
        new Vector4(-0.6387863f,  -0.3999016f, 0.65729f),
        new Vector4(-0.891588f,  -0.115146f, 0.437964f),
        new Vector4(-0.775663f,  0.0194654f, 0.630848f),
        new Vector4(-0.5360528f,  -0.1828935f, 0.824134f),
        new Vector4(-0.513927f,  -0.000130296f, 0.857834f),
        new Vector4(-0.4368436f,  -0.2831443f, 0.853813f),
        new Vector4(-0.1794069f,  -0.4226944f, 0.888337f),
        new Vector4(-0.00183062f,  -0.4371257f, 0.899398f),
        new Vector4(-0.2598701f,  -0.1719497f, 0.950211f),
        new Vector4(-0.08650014f,  -0.004176182f, 0.996243f),
        new Vector4(0.006921067f,  -0.001478712f, 0.999975f),
        new Vector4(0.05654667f,  -0.9351676f, 0.349662f),
        new Vector4(0.1168661f,  -0.754741f, 0.64553f),
        new Vector4(0.3534952f,  -0.7472929f, 0.562667f),
        new Vector4(0.1635596f,  -0.5863093f, 0.793404f),
        new Vector4(0.5910167f,  -0.786864f, 0.177609f),
        new Vector4(0.5820105f,  -0.5659724f, 0.5839f),
        new Vector4(0.7254612f,  -0.5323696f, 0.436221f),
        new Vector4(0.4016336f,  -0.4329237f, 0.807012f),
        new Vector4(0.5287027f,  -0.4064075f, 0.745188f),
        new Vector4(0.314015f,  -0.2375291f, 0.919225f),
        new Vector4(0.02922117f,  -0.2097672f, 0.977315f),
        new Vector4(0.4201531f,  -0.1445212f, 0.895871f),
        new Vector4(0.2821195f,  -0.01079273f, 0.959319f),
        new Vector4(0.7152653f,  -0.1972963f, 0.670425f),
        new Vector4(0.8167331f,  -0.1217311f, 0.564029f),
        new Vector4(0.8517836f,  0.01290532f, 0.523735f),
        new Vector4(-0.657816f,  0.134013f, 0.74116f),
        new Vector4(-0.851676f,  0.321285f, 0.414033f),
        new Vector4(-0.603183f,  0.361627f, 0.710912f),
        new Vector4(-0.6607267f,  0.5282444f, 0.533289f),
        new Vector4(-0.323619f,  0.182656f, 0.92839f),
        new Vector4(-0.2080927f,  0.1494067f, 0.966631f),
        new Vector4(-0.4205947f,  0.4184987f, 0.804959f),
        new Vector4(-0.06831062f,  0.3712724f, 0.926008f),
        new Vector4(-0.165943f,  0.5029928f, 0.84821f),
        new Vector4(-0.6137413f,  0.7001954f, 0.364758f),
        new Vector4(-0.3009551f,  0.6550035f, 0.693107f),
        new Vector4(-0.1356791f,  0.6460465f, 0.751143f),
        new Vector4(-0.3677429f,  0.7920387f, 0.487278f),
        new Vector4(-0.08688695f,  0.9677781f, 0.236338f),
        new Vector4(0.07250954f,  0.1327261f, 0.988497f),
        new Vector4(0.5244588f,  0.05565827f, 0.849615f),
        new Vector4(0.2498424f,  0.3364912f, 0.907938f),
        new Vector4(0.2608168f,  0.5340923f, 0.804189f),
        new Vector4(0.3888291f,  0.3207975f, 0.863655f),
        new Vector4(0.6413552f,  0.1619097f, 0.749966f),
        new Vector4(0.8523082f,  0.2647078f, 0.451111f),
        new Vector4(0.5591328f,  0.3038472f, 0.771393f),
        new Vector4(0.9147445f,  0.3917669f, 0.0987938f),
        new Vector4(0.08110893f,  0.7317293f, 0.676752f),
        new Vector4(0.3154335f,  0.7388063f, 0.59554f),
        new Vector4(0.1677455f,  0.9625717f, 0.212877f),
        new Vector4(0.3015989f,  0.9509261f, 0.069128f),
        new Vector4(0.5600207f,  0.5649592f, 0.605969f),
        new Vector4(0.6455291f,  0.7387806f, 0.193637f)};


        private const float sampSize2 = 1.31512f;
        private const float sampSize4 = 2.61496f;
        private const float sampSize8 = 4.80795f;
        private const float sampSize16 = 10.607f;
        private const float sampSize32 = 20.393f;
        private const float sampSize64 = 41.4819f;

#endregion
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(Colorbleed))]
    public class ColorbleedEditor : Editor
    {
        private readonly GUIContent[] qualityTexts = new GUIContent[6] {
            new GUIContent("Very Low (2 samples)"),
            new GUIContent("Low (4 samples)"),
            new GUIContent("Medium (8 samples)"),
            new GUIContent("High (16 samples)"),
            new GUIContent("Very High (32 samples)"),
            new GUIContent("Ultra (64 samples)")
        };
        private readonly int[] qualityInts = new int[6] { 2, 4, 8, 16, 32, 64 };
        private readonly GUIContent[] qualityTexts2 = new GUIContent[5] {
            new GUIContent("Very Low (2 samples) Adaptive"),
            new GUIContent("Low (4 samples) Adaptive"),
            new GUIContent("Medium (8 samples) Adaptive"),
            new GUIContent("High (16 samples) Adaptive"),
            new GUIContent("Very High (32 samples) Adaptive")
        };
        private readonly int[] qualityInts2 = new int[5] { 2, 4, 8, 16, 32 };

        private readonly GUIContent[] downsamplingTexts = new GUIContent[3] {
            new GUIContent("Off"),
            new GUIContent("2x"),
            new GUIContent("4x")
        };
        private readonly int[] downsamplingInts = new int[3] { 1, 2, 4 };
        private bool lumaFoldout = true;
        // private bool colorBleedFoldout = false;
        private bool optimizationFoldout = true;
        private bool pipelineFoldout = true;
        private bool blurFoldout = true;
        private bool aboutFoldout = false;

		private readonly GUIContent raymarchingStepsLabelContent = new GUIContent("Raymarching Steps:", "Number of depth queries for each sample along its ray");

        private readonly GUIContent radiusLabelContent = new GUIContent("Radius:", "Distance of the objects that are still considered for color bleeding");
        private readonly GUIContent powerLabelContent = new GUIContent("Power:", "Strength of the color bleeding");
        private readonly GUIContent colorBleedPresenceLabelContent = new GUIContent("Presence:", "Smoothly limits maximal saturation of color bleed");
        private readonly GUIContent colorBleedDarknessLabelContent = new GUIContent("Brightness:", "Suppresses dark colors");
        
        private readonly GUIContent saturationLabelContent = new GUIContent("Saturation:", "Saturation of color bleeding");
        private readonly GUIContent adaptiveLevelLabelContent = new GUIContent("Adaptive Offset:", "Adjust to fine-tune adaptive sampling quality/performance");
        private readonly GUIContent qualityLabelContent = new GUIContent("Quality:", "Number of samples used");
        private readonly GUIContent downsamplingLabelContent = new GUIContent("Downsampling:", "Reduces the resulting texture size");

        private readonly GUIContent lumaSensitivityLabelContent = new GUIContent("Luminance Sensitivity Settings", "Lets you control which bright/shining surfaces do not show color bleeding");
        private readonly GUIContent lumaEnabledLabelContent = new GUIContent("Luma Sensitivity:", "Enables luminance sensitivity");
        private readonly GUIContent lumaThresholdLabelContent = new GUIContent("Threshold:", "Sets which bright surfaces no longer show color bleeding");
        private readonly GUIContent lumaThresholdHDRLabelContent = new GUIContent("Threshold (HDR):", "Sets which bright surfaces no longer show color bleeding");
        private readonly GUIContent lumaWidthLabelContent = new GUIContent("Falloff Width:", "Controls the weight of the color bleeding as it nears the threshold");
        private readonly GUIContent lumaSoftnessLabelContent = new GUIContent("Falloff Softness:", "Controls the gradient of the falloff");
        
        private readonly GUIContent luminanceModeLabelContent = new GUIContent("Mode:", "Switches sensitivity between luma and HSV value");
        private readonly GUIContent optimizationLabelContent = new GUIContent("Performance Settings:", "");
        private readonly GUIContent doomModeLabelContent = new GUIContent("Downsampled Pre-pass:", "Enable to boost performance, especially on lower radius and higher resolution/quality settings. Greedy option is faster but might produce minute detail loss.");
        private readonly GUIContent adaptiveSamplingLabelContent = new GUIContent("Adaptive Sampling:", "Automagically sets progressively lower quality for distant geometry");

        private readonly GUIContent pipelineLabelContent = new GUIContent("Rendering Pipeline:", "");
        private readonly GUIContent commandBufferLabelContent = new GUIContent("Command Buffer:", "Insert effect via command buffer (BeforeImageEffectsOpaque event)");
        private readonly GUIContent gBufferLabelContent = new GUIContent("GBuffer Depth&Normals:", "Take depth&normals from GBuffer of deferred rendering path, use this to overcome compatibility issues or for better precision");

        private readonly GUIContent blurLabelContent = new GUIContent("Enable Blur:", "Uncheck to disable built-in blur effect");
        private readonly GUIContent blurFoldoutContent = new GUIContent("Blur Settings", "Lets you control blur behaviour");
        private readonly GUIContent blurSizeContent = new GUIContent("Blur Size:", "Change to adjust the size of area that is averaged");
        private readonly GUIContent blurDeviationContent = new GUIContent("Blur Sharpness:", "Standard deviation for Gaussian blur - smaller deviation means sharper image");

        private readonly GUIContent cbLabelContent = new GUIContent("Show Only Colorbleed:", "Displays just the color bleeding - used for tuning the settings");

        private readonly GUIContent backfaceLabelContent = new GUIContent("Limit Backfaces:", "Limits bleeding on surfaces behind the source");
		private readonly GUIContent backfaceDegreeLabelContent = new GUIContent("Backface Suppression:", "Degree of suppression");

        private GraphWidget lumaGraphWidget;
        private GraphWidgetDrawingParameters lumaGraphWidgetParams;
        private float lastLumaTreshold;
        private float lastLumaKneeWidth;
        private float lastLumaKneeLinearity;
        private float lastLumaMaxFx;
        private bool lastLumaSensitive;
        private float lumaMaxFx = 10.0f;
        private bool isHDR;
        private Camera camera;

        private GraphWidgetDrawingParameters GetLumaGraphWidgetParameters(Colorbleed cbScript)
        {
            if (lumaGraphWidgetParams != null &&
                lastLumaTreshold == cbScript.LumaTreshold &&
                lastLumaKneeWidth == cbScript.LumaKneeWidth &&
                lastLumaMaxFx == lumaMaxFx &&
                lastLumaKneeLinearity == cbScript.LumaKneeLinearity) return lumaGraphWidgetParams;

            lastLumaTreshold = cbScript.LumaTreshold;
            lastLumaKneeWidth = cbScript.LumaKneeWidth;
            lastLumaKneeLinearity = cbScript.LumaKneeLinearity;
            lastLumaMaxFx = lumaMaxFx;

            lumaGraphWidgetParams = new GraphWidgetDrawingParameters()
            {
                GraphSegmentsCount = 128,
                GraphColor = Color.white,
                GraphThickness = 2.0f,
                GraphFunction = ((float x) =>
                {
                    float Y = (x - (cbScript.LumaTreshold - cbScript.LumaKneeWidth)) * (1.0f / (2.0f * cbScript.LumaKneeWidth));
                    x = Mathf.Min(1.0f, Mathf.Max(0.0f, Y));
                    return ((-Mathf.Pow(x, cbScript.LumaKneeLinearity) + 1));
                }),
                YScale = 0.65f,
                MinY = 0.1f,
                MaxFx = lumaMaxFx,
                GridLinesXCount = 4,
                Lines = new List<GraphWidgetLine>()
                {
                    new GraphWidgetLine() {
                        Color = Color.red,
                        Thickness = 2.0f,
                        From = new Vector3(cbScript.LumaTreshold / lumaMaxFx, 0.0f, 0.0f),
                        To = new Vector3(cbScript.LumaTreshold / lumaMaxFx, 1.0f, 0.0f)
                    },
                    new GraphWidgetLine() {
                        Color = Color.blue * 0.7f,
                        Thickness = 2.0f,
                        From = new Vector3((cbScript.LumaTreshold - cbScript.LumaKneeWidth) / lumaMaxFx, 0.0f, 0.0f),
                        To = new Vector3((cbScript.LumaTreshold - cbScript.LumaKneeWidth) / lumaMaxFx, 1.0f, 0.0f)
                    },
                    new GraphWidgetLine() {
                        Color = Color.blue * 0.7f,
                        Thickness = 2.0f,
                        From = new Vector3((cbScript.LumaTreshold + cbScript.LumaKneeWidth) / lumaMaxFx, 0.0f, 0.0f),
                        To = new Vector3((cbScript.LumaTreshold + cbScript.LumaKneeWidth) / lumaMaxFx, 1.0f, 0.0f)
                    }
                }
            };

            return lumaGraphWidgetParams;
        }


        private void SetIcon()
        {
            try
            {
                Texture2D icon = (Texture2D)Resources.Load("script_icon");
                Type editorGUIUtilityType = typeof(UnityEditor.EditorGUIUtility);
                System.Reflection.BindingFlags bindingFlags = System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
                object[] args = new object[] { target, icon };
                editorGUIUtilityType.InvokeMember("SetIconForObject", bindingFlags, null, null, args);
            }
            catch (Exception ex)
            {
                if (Debug.isDebugBuild) Debug.Log("Colorbleed Effect Error: There was an exception while setting icon to Colorbleed script: " + ex.Message);
            }
        }

        void OnEnable()
        {
            lumaGraphWidget = new GraphWidget();
            camera = (target as Colorbleed).GetComponent<Camera>();
            if (camera != null) isHDR = camera.allowHDR;
            SetIcon();
        }

        override public void OnInspectorGUI()
        {
            var cbScript = target as Colorbleed;
            cbScript.colorbleedShader = EditorGUILayout.ObjectField("Colorbleed Shader", cbScript.colorbleedShader, typeof(Shader), false) as UnityEngine.Shader;

            EditorGUILayout.Space();

            cbScript.Algorithm = (Colorbleed.ColorbleedAlgorithmType)EditorGUILayout.EnumPopup("Algorithm", cbScript.Algorithm);
			if (cbScript.Algorithm == Colorbleed.ColorbleedAlgorithmType.Raymarching){
				cbScript.RaymarchStepsCount = EditorGUILayout.IntSlider(raymarchingStepsLabelContent, cbScript.RaymarchStepsCount, 2, 16);
			}
			EditorGUILayout.Space();

			cbScript.Radius = EditorGUILayout.FloatField(radiusLabelContent, cbScript.Radius);
            cbScript.ColorBleedPower = EditorGUILayout.FloatField(powerLabelContent, cbScript.ColorBleedPower);
            cbScript.ColorBleedPresence = EditorGUILayout.Slider(colorBleedPresenceLabelContent, cbScript.ColorBleedPresence, 0.0f, 1.0f);
            cbScript.ColorBleedBrightness = EditorGUILayout.Slider(colorBleedDarknessLabelContent, cbScript.ColorBleedBrightness, 0.0f, 1.0f);
            cbScript.Saturation = EditorGUILayout.Slider(saturationLabelContent, cbScript.Saturation, 0.0f, 1.0f);

            EditorGUILayout.Space();
            cbScript.GiBackfaces = EditorGUILayout.Toggle(backfaceLabelContent, cbScript.GiBackfaces);
            if(cbScript.GiBackfaces){
				cbScript.BackfacesRadiusMultiplier = EditorGUILayout.Slider(backfaceDegreeLabelContent, cbScript.BackfacesRadiusMultiplier, 0.0f, 1.0f);
            }
            EditorGUILayout.Space();
            
            EditorGUI.indentLevel++;
            optimizationFoldout = EditorGUI.Foldout(EditorGUILayout.GetControlRect(), optimizationFoldout, optimizationLabelContent, true, EditorStyles.foldout);

            if (optimizationFoldout)
            {
				if (cbScript.AdaptiveType == Colorbleed.AdaptiveSamplingType.Disabled)
				{
					cbScript.Quality = EditorGUILayout.IntPopup(qualityLabelContent, cbScript.Quality, qualityTexts, qualityInts);
				}
				else
				{
					cbScript.Quality = EditorGUILayout.IntPopup(qualityLabelContent, cbScript.Quality, qualityTexts2, qualityInts2);
				}

                cbScript.AdaptiveType = (Colorbleed.AdaptiveSamplingType)EditorGUILayout.EnumPopup(adaptiveSamplingLabelContent, cbScript.AdaptiveType);
                if (cbScript.AdaptiveType == Colorbleed.AdaptiveSamplingType.EnabledManual)
                {
                    cbScript.AdaptiveQualityCoefficient = EditorGUILayout.Slider(adaptiveLevelLabelContent, cbScript.AdaptiveQualityCoefficient, 0.5f, 2.0f);
                }
                cbScript.DoomMode = (Colorbleed.DoomModeType)EditorGUILayout.EnumPopup(doomModeLabelContent, cbScript.DoomMode);
                cbScript.Downsampling = EditorGUILayout.IntPopup(downsamplingLabelContent, cbScript.Downsampling, downsamplingTexts, downsamplingInts);
            }
            EditorGUILayout.Space();

            pipelineFoldout = EditorGUI.Foldout(EditorGUILayout.GetControlRect(), pipelineFoldout, pipelineLabelContent, true, EditorStyles.foldout);
            if (pipelineFoldout)
            {
                cbScript.CommandBufferEnabled = EditorGUILayout.Toggle(commandBufferLabelContent, cbScript.CommandBufferEnabled);
                cbScript.UseGBuffer = EditorGUILayout.Toggle(gBufferLabelContent, cbScript.UseGBuffer);
                if (cbScript.UseGBuffer)
                {
                    EditorGUILayout.HelpBox("This may cause performance drop on some configurations, turn off when not needed.", MessageType.Warning);
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            cbScript.IsLumaSensitive = EditorGUILayout.Toggle(lumaEnabledLabelContent, cbScript.IsLumaSensitive);

            if (cbScript.IsLumaSensitive)
            {
                if (lastLumaSensitive == false) lumaFoldout = true;

                EditorGUILayout.Space();
                EditorGUI.indentLevel++;
                lumaFoldout = EditorGUI.Foldout(EditorGUILayout.GetControlRect(), lumaFoldout, lumaSensitivityLabelContent, true, EditorStyles.foldout);

                if (lumaFoldout)
                {
                    cbScript.LuminanceMode = (Colorbleed.LuminanceModeType)EditorGUILayout.EnumPopup(luminanceModeLabelContent, cbScript.LuminanceMode);

                    lumaMaxFx = 1.0f;
                    float tresholdMax = lumaMaxFx;
                    float kneeWidthMax = lumaMaxFx;
                    GUIContent tresholdLabel = lumaThresholdLabelContent;
                    if (camera != null)
                    {
                        if (camera.allowHDR)
                        {
                            lumaMaxFx = 10.0f;
                            tresholdMax = lumaMaxFx;
                            kneeWidthMax = lumaMaxFx;
                            tresholdLabel = lumaThresholdHDRLabelContent;

                            if (!isHDR)
                            {
                                cbScript.LumaTreshold *= 10.0f;
                                cbScript.LumaKneeWidth *= 10.0f;
                                isHDR = true;
                            }
                        }
                        else
                        {
                            if (isHDR)
                            {
                                cbScript.LumaTreshold *= 0.1f;
                                cbScript.LumaKneeWidth *= 0.1f;
                                isHDR = false;
                            }
                        }
                    }

                    cbScript.LumaTreshold = EditorGUILayout.Slider(tresholdLabel, cbScript.LumaTreshold, 0.0f, tresholdMax);
                    cbScript.LumaKneeWidth = EditorGUILayout.Slider(lumaWidthLabelContent, cbScript.LumaKneeWidth, 0.0f, kneeWidthMax);
                    cbScript.LumaKneeLinearity = EditorGUILayout.Slider(lumaSoftnessLabelContent, cbScript.LumaKneeLinearity, 1.0f, 10.0f);
                    EditorGUILayout.Space();
                    lumaGraphWidget.Draw(GetLumaGraphWidgetParameters(cbScript));
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            // EditorGUILayout.Space();

            cbScript.DoBlur = EditorGUILayout.Toggle(blurLabelContent, cbScript.DoBlur);
            if (cbScript.DoBlur)
            {
                EditorGUILayout.Space();
                EditorGUI.indentLevel++;
                blurFoldout = EditorGUI.Foldout(EditorGUILayout.GetControlRect(), blurFoldout, blurFoldoutContent, true, EditorStyles.foldout);
                if (blurFoldout)
                {
                    cbScript.BlurSize = EditorGUILayout.IntSlider(blurSizeContent, cbScript.BlurSize, 3, 17);
                    cbScript.Deviation = EditorGUILayout.Slider(blurDeviationContent, cbScript.Deviation, 0.01f, 3.0f);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            cbScript.OutputCBOnly = EditorGUILayout.Toggle(cbLabelContent, cbScript.OutputCBOnly);
            EditorGUILayout.Space();

            aboutFoldout = EditorGUI.Foldout(EditorGUILayout.GetControlRect(), aboutFoldout, "About", true, EditorStyles.foldout);
            if (aboutFoldout)
            {
                EditorGUILayout.HelpBox("Colorbleed v1.0 by Project Wilberforce.\n\nThank you for your purchase, if you have any issues or suggestions, feel free to contact us at <projectwilberforce@gmail.com>.\n\nPlease rate the plugin on Asset Store as it really helps further development.", MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Rate on Asset Store"))
                {
                    Application.OpenURL("https://www.assetstore.unity3d.com/en/#!/account/downloads/search=Colorbleed");
                }
                if (GUILayout.Button("Send Feedback"))
                {
                    Application.OpenURL("mailto:projectwilberforce@gmail.com");
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUI.changed)
            {
                // Force parameters to be positive
                cbScript.Radius = Mathf.Clamp(cbScript.Radius, 0.001f, float.MaxValue);
                cbScript.ColorBleedPower = Mathf.Clamp(cbScript.ColorBleedPower, 0, float.MaxValue);

                if (cbScript.Quality == 64 && cbScript.AdaptiveType != Colorbleed.AdaptiveSamplingType.Disabled)
                {
                    cbScript.Quality = 32;
                }

                if (cbScript.BlurSize % 2 == 0)
                {
                    cbScript.BlurSize += 1;
                }

                // Mark as dirty
                EditorUtility.SetDirty(target);
            }
            Undo.RecordObject(target, "Colorbleed change");

            lastLumaSensitive = cbScript.IsLumaSensitive;
        }
    }

    public class GraphWidgetLine
    {
        public Vector3 From { get; set; }
        public Vector3 To { get; set; }
        public Color Color { get; set; }
        public float Thickness { get; set; }
    }

    public class GraphWidgetDrawingParameters
    {

        public IList<GraphWidgetLine> Lines { get; set; }

        /// <summary>
        /// Number of line segments that will be used to approximate function shape
        /// </summary>
        public uint GraphSegmentsCount { get; set; }

        /// <summary>
        /// Function to draw (X -> Y) 
        /// </summary>
        public Func<float, float> GraphFunction { get; set; }

        public Color GraphColor { get; set; }
        public float GraphThickness { get; set; }

        public float YScale { get; internal set; }
        public float MinY { get; internal set; }

        public int GridLinesXCount { get; set; }
        public float MaxFx { get; internal set; }
    }

    public class GraphWidget
    {
        private Vector3[] transformedLinePoints = new Vector3[2];
        private Vector3[] graphPoints;

        void TransformToRect(Rect rect, ref Vector3 v)
        {
            v.x = Mathf.Lerp(rect.x, rect.xMax, v.x);
            v.y = Mathf.Lerp(rect.yMax, rect.y, v.y);
        }

        private void DrawLine(Rect rect, float x1, float y1, float x2, float y2, Color color)
        {
            transformedLinePoints[0].x = x1;
            transformedLinePoints[0].y = y1;
            transformedLinePoints[1].x = x2;
            transformedLinePoints[1].y = y2;

            TransformToRect(rect, ref transformedLinePoints[0]);
            TransformToRect(rect, ref transformedLinePoints[1]);

            Handles.color = color;
            Handles.DrawPolyLine(transformedLinePoints);
        }

        private void DrawAALine(Rect rect, float thickness, float x1, float y1, float x2, float y2, Color color)
        {
            transformedLinePoints[0].x = x1;
            transformedLinePoints[0].y = y1;
            transformedLinePoints[1].x = x2;
            transformedLinePoints[1].y = y2;

            TransformToRect(rect, ref transformedLinePoints[0]);
            TransformToRect(rect, ref transformedLinePoints[1]);

            Handles.color = color;
            Handles.DrawPolyLine(transformedLinePoints);
        }

        public void Draw(GraphWidgetDrawingParameters drawingParameters)
        {
            Rect bgRect = GUILayoutUtility.GetRect(128, 70);

            Handles.DrawSolidRectangleWithOutline(bgRect, Color.grey, Color.black);

            // Draw grid lines
            Color gridColor = Color.black * 0.1f;
            DrawLine(bgRect, 0.0f, drawingParameters.MinY + drawingParameters.YScale,
                             1.0f, drawingParameters.MinY + drawingParameters.YScale, gridColor);

            DrawLine(bgRect, 0.0f, drawingParameters.MinY,
                             1.0f, drawingParameters.MinY, gridColor);

            float gridXStep = 1.0f / (drawingParameters.GridLinesXCount + 1);
            float gridX = gridXStep;
            for (int i = 0; i < drawingParameters.GridLinesXCount; i++)
            {
                DrawLine(bgRect, gridX, 0.0f,
                                 gridX, 1.0f, gridColor);

                gridX += gridXStep;
            }

            if (drawingParameters.GraphSegmentsCount > 0)
            {
                if (graphPoints == null || graphPoints.Length < drawingParameters.GraphSegmentsCount + 1)
                    graphPoints = new Vector3[drawingParameters.GraphSegmentsCount + 1];

                float x = 0.0f;
                float xStep = 1.0f / drawingParameters.GraphSegmentsCount;

                for (int i = 0; i < drawingParameters.GraphSegmentsCount + 1; i++)
                {
                    float y = drawingParameters.GraphFunction(x * drawingParameters.MaxFx);

                    y *= drawingParameters.YScale;
                    y += drawingParameters.MinY;

                    graphPoints[i].x = x;
                    graphPoints[i].y = y;
                    TransformToRect(bgRect, ref graphPoints[i]);
                    x += xStep;
                }

                Handles.color = drawingParameters.GraphColor;
                Handles.DrawAAPolyLine(drawingParameters.GraphThickness, graphPoints);
            }

            if (drawingParameters != null && drawingParameters.Lines != null)
            {
                foreach (var line in drawingParameters.Lines)
                {
                    DrawAALine(bgRect, line.Thickness, line.From.x, line.From.y, line.To.x, line.To.y, line.Color);
                }
            }

            // Label
            Vector3 labelPosition = new Vector3(0.01f, 0.99f);
            TransformToRect(bgRect, ref labelPosition);
            Handles.Label(labelPosition, "Luminance sensitivity curve", EditorStyles.miniLabel);

        }

    }

#endif
}
