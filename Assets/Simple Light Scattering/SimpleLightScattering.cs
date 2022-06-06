using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class SimpleLightScattering : MonoBehaviour
{
    private static Vector2 BlurV = new Vector2(0, 1);
    private static Vector2 BlurH = new Vector2(1, 0);
      
    private Camera cam;

    [Tooltip("Put there component that you added to your Directional Light, without this effect cannot work properly!")]
    public DirectionalLightAttachment attachment;

    [Tooltip("Render this effect to screen?")]
    public bool EffectEnabled = true;

    [Tooltip("Parameter controlling technique used to make low raySteps values look good, values between 2-4 are probably good for you. It offsets every ray start position with small offset to achieve" +
        " higher precision with same amount of rays (so a lot better looking effect)")]
    [Range(2, 8)]
    public int InterleavedPositionGridSize = 4;

    [Tooltip("How many times resolution of volumetric light texture should be divided, 2 is the sweet spot for performance and looks, however in some sceneries 4 still looks good" +
        "and gives much better performance")]
    [Range(1, 8)]
    public int Downsample = 2;
    private int LastDownsample;

    [Tooltip("Amount of passes to blur the texture with volumetric light, more passes = better quality but worse performance.")]
    [Range(0, 8)]
    public int BlurPasses = 2;
    [Tooltip("Parameter used in blur pass, used to smooth out the transition between background and edges of geometry (to avoid hard cutoff on edges). " +
        "Value you want here depends on the type of scenery you have for some kind of far mountains you probably want value like 1.5, for small objects high values like 15")]
    public float BlurDepthFalloff = 11f;
    [Tooltip("Parameter used in upscaling volumetric light texture process to decide when to use the sample from the texture with linear or nearest filter. " +
        "Current value of 0.01 should be good for most cases, if you want to fine-tune it you probably want to have small values here like 0.01")]
    public float DepthThreshold = 0.01f;

    [Tooltip("Maximum distance that rays from the camera can travel. Usually you don't want to have this value bigger than your shadows distance.")]
    public int MaxRayDistance = 1000;

    [Tooltip("How many steps each ray can take, the most performance painful parameter, keep it as low as possible for best performance. However low value between 5 - 10 should be enough for most purposes.")]
    [Range(5, 50)]
    public int StepsPerRay = 10;

    [Header("Sunshafts basic settings")]

    [Tooltip("Use defined color here or take color from directional light object?")]
    public bool UseDirectionalLightColor = true;
    public Color LightColor = Color.white;
    [Tooltip("Color of fog when is in shadow")]
    public Color ShadowedFogColor = Color.gray;

    [Tooltip("Makes the effect stronger, describes how much of light is reflected towards camera.")]
    [Range(0, 1)]
    public float ScatteringCoeff = 0.2f;
    [Tooltip("Controls how light is absorbed with distance, high value makes objects lose color with distance and fade into fog. Helpfull to make fog look more dense, to make scene darker.")]
    [Range(0, 2)]
    public float ExtinctionCoeff = 0.2f;
    [Range(0, 1)]
    [Tooltip("Helps to control how much ExtinctionCoeff is affecting skybox, helps to avoid situations where skybox is too foggy.")]
    public float SkyboxExtinctionCoeff = 0.0f;

    [Tooltip("Base value for fog density used in calculactions. This value affects density of all kinds of fogs.")]
    [Range(0, 100)]
    public float GlobalFogDensity = 3.3f;

    [Header("Mie Scattering")]
    [Tooltip("Mie scattering - I'am using Rayleigh approximation, basically this effect describes how much light reflection depends on light direction." +
        "This effect enabled with high MieG parameter and MieScatteringBase set to low makes scene look like all light is only from sun, helps to get more realistic looks.")]
    public bool MieScatteringEnabled = true;
    [Tooltip("Used to avoid effect where Mie Scattering would make all fog in shadow look black or invisible, it helps to achieve kind of ambient light for fog.")]
    [Range(0, 1)]
    public float MieScatteringBase = 0.62f;
    [Tooltip("Parameter that controls strenght of Mie Scattering effect.")]
    [Range(0, 1)]
    public float MieG = 0.6f;

    [Header("Height Fog")]
    [Tooltip("Effect simulating earth surface getting darker and darker when camera altitude increases.")]
    public bool HeightFogEnabled = true;

    [Tooltip("Level 0 of earth surface, terrain, water level")]
    public float HeightFogGroundLevel = 25f;

    [Tooltip("How fast surface under us is getting darker over increasing altitude.")]
    [Range(0, 1)]
    public float HeightFogScale = 0.4f;

    [Header("Volumetric fog global settings")]
    [Tooltip("Effect controlling if GlobalAnimatedFog and Dense Fog is enabled.")]
    public bool VolumetricFogEnabled = true;

    [Header("Global Animated Fog")]
    [Tooltip("Effect simulating dense fog with wind adds a lot of dynamism to scene.")]
    public bool GlobalAnimatedFogEnabled = true;

    [Tooltip("Controls how much noise used to simulate global fog is affecting global FogDensity value.")]
    public float FogDensityMultipler = 1.1f;
    [Tooltip("Controls size of particles in fog.")]
    public float FogNoiseScale = 18f;
    [Tooltip("Direction and speed of fog.")]
    public Vector3 FogMoveSpeed = new Vector3(50, 20, 10);

    [Header("Dense Height Dependent Volumetric Fog")]
    [Tooltip("Dense type of fog that sticts to fixed height in world.")]
    public bool VolumetricDenseFogEnabled = true;

    [Tooltip("2 dimensional noise texture to create waves of fog.")]
    public Texture DenseFogNoiseTex;
    [Tooltip("Direction and speed of fog.")]
    public Vector2 DenseFogMoveSpeed = new Vector2(0.5f, 0);
    [Tooltip("Scale of noise texture used to deform fog top layer.")]
    public float DenseFogNoiseScale = 200f;
    [Tooltip("Value by which GlobalFogDensity will be multiplied when dense fog will be rendered.")]
    public float DenseFogDensityMultipler = 4f;

    [Space(15)]
    [Tooltip("Color of dense fog.")]
    public Color DenseHeightFogColor = Color.white;
    [Tooltip("Color of dense fog when in shadow.")]
    public Color DenseHeightShadowedFogColor = Color.gray;
    [Tooltip("Altitude at which dense fog starts")]
    public float DenseHeightFogStart = 8f;
    [Tooltip("Minimal height of dense fog. Final height of fog is this value plus DenseHeightFogNoiseMaxHeight * currentNoiseValue (local var in shader).")]
    public float DenseHeightFogHeight = 20f;
    [Tooltip("Maximum addition to DenseHeightFogHeight that can occur. Final height of fog is DenseHeightFogHeight value plus this value * currentNoiseValue (local var in shader).")]
    public float DenseHeightFogNoiseMaxHeight = 26f;

    private void Start()
    {
        cam = GetComponent<Camera>();
        LastDownsample = Downsample;

        if (attachment == null)
        {
            Debug.LogError("You have not attached DirectionalLightAttachment to this script! You have to do this! For now I will just pick first DirectionalLightAttachment I will find!");
            attachment = (DirectionalLightAttachment)FindObjectOfType(typeof(DirectionalLightAttachment));

            if (attachment == null)
            {
                Debug.LogError("Haven't found any DirectionalLightAttachment in whole scene, you need to add one to your Directional Light!");
            }
        }

        attachment.InitRendering(Downsample, cam);
    }
      
    // Update is called once per frame
    void Update()
    {
        if (LastDownsample != Downsample)
        {
            attachment.ChangeCamera(Downsample, cam); //reinit directional light rendering because we have to recreate render targets (their resolution changed)
            LastDownsample = Downsample;
        }

        //Update uniforms of LS
        Color lColor = (UseDirectionalLightColor) ? attachment.DirectionalLightColor : LightColor;
        attachment.LSMaterial.SetColor("LightColor", lColor);
        attachment.LSMaterial.SetColor("ShadowedFogColor", ShadowedFogColor);

        attachment.LSMaterial.SetFloat("MaxRayDistance", MaxRayDistance);
        attachment.LSMaterial.SetFloat("StepsPerRay", StepsPerRay);

        attachment.LSMaterial.SetFloat("InterleavedPosGridSize", InterleavedPositionGridSize);
        attachment.LSMaterial.SetFloat("ScatteringCoefficient", ScatteringCoeff / 100f);
        attachment.LSMaterial.SetFloat("ExtinctionCoefficient", ExtinctionCoeff / 100f);
        attachment.LSMaterial.SetFloat("SkyboxExtinctionCoefficient", SkyboxExtinctionCoeff);
        attachment.LSMaterial.SetFloat("FogDensity", GlobalFogDensity);

        //MIE SCATTERING
        UpdateKeyword(MieScatteringEnabled, "MIE_SCATTERING");

        attachment.LSMaterial.SetFloat("MieScatteringBase", MieScatteringBase);
        attachment.LSMaterial.SetFloat("MieG", MieG);

        //HEIGHT FOG
        UpdateKeyword(HeightFogEnabled, "HEIGHT_FOG");

        attachment.LSMaterial.SetFloat("HeightFogScale", HeightFogScale / -100f);
        attachment.LSMaterial.SetFloat("HeightFogGroundLevel", HeightFogGroundLevel);

        //GLOBAL VOLUM FOG SETTINGS
        UpdateKeyword(VolumetricFogEnabled, "VOLUMETRIC_FOG");

        //GLOBAL ANIMATED FOG
        UpdateKeyword(GlobalAnimatedFogEnabled, "VOLUMETRIC_GLOBAL_FOG");
        attachment.LSMaterial.SetFloat("VolumetricFogDensityMultipler", FogDensityMultipler);
        attachment.LSMaterial.SetFloat("VolumetricFogNoiseScale", FogNoiseScale);
        attachment.LSMaterial.SetVector("VolumetricFogMoveDir", FogMoveSpeed);

        //VOLUMETRIC DENSE FOG
        UpdateKeyword(VolumetricDenseFogEnabled, "VOLUMETRIC_DENSE_FOG");

        attachment.LSMaterial.SetTexture("VolumetricDenseFogNoiseTex", DenseFogNoiseTex);
        attachment.LSMaterial.SetVector("VolumetricDenseFogMoveDir", DenseFogMoveSpeed);
        attachment.LSMaterial.SetFloat("VolumetricDenseFogNoiseScale", DenseFogNoiseScale);
        attachment.LSMaterial.SetFloat("VolumetricDenseFogDensityMultipler", DenseFogDensityMultipler);

        attachment.LSMaterial.SetColor("VolumetricDenseHeightFogColor", DenseHeightFogColor);
        attachment.LSMaterial.SetColor("VolumetricDenseHeightShadowedFogColor", DenseHeightShadowedFogColor);

        attachment.LSMaterial.SetFloat("VolumetricDenseHeightFogStart", DenseHeightFogStart);
        attachment.LSMaterial.SetFloat("VolumetricDenseHeightFogHeight", DenseHeightFogHeight);
        attachment.LSMaterial.SetFloat("VolumetricDenseHeightFogNoiseMaxHeight", DenseHeightFogNoiseMaxHeight);
    }

    private void UpdateKeyword(bool enabled, string name)
    {
        if (enabled != attachment.LSMaterial.IsKeywordEnabled(name))
        {
            if (enabled) attachment.LSMaterial.EnableKeyword(name);
            else attachment.LSMaterial.DisableKeyword(name);
        }
    }

    [ImageEffectOpaque]
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (attachment && attachment.LSMaterial && attachment.DownscaleMaterial && attachment.CombineMaterial && EffectEnabled)
        { 
            //Biliteral Blur preparation, create render targets
            RenderTextureFormat format = RenderTextureFormat.ARGBHalf;
            RenderTexture volumLightBlurV = RenderTexture.GetTemporary(attachment.VolumetricLightTarget.descriptor.width, attachment.VolumetricLightTarget.descriptor.height, 0, format);
            RenderTexture volumLightBlurH = RenderTexture.GetTemporary(attachment.VolumetricLightTarget.descriptor.width, attachment.VolumetricLightTarget.descriptor.height, 0, format);

            volumLightBlurV.filterMode = FilterMode.Bilinear;
            volumLightBlurH.filterMode = FilterMode.Bilinear;

            attachment.BlurMaterial.SetFloat("BlurDepthFalloff", BlurDepthFalloff);
            attachment.BlurMaterial.SetTexture("LowResolutionDepth", attachment.DownscaledDepthTarget);

            Graphics.Blit(attachment.VolumetricLightTarget, volumLightBlurH);

            //4x4 per pass biliteral blur
            for (int i = 0; i < BlurPasses; i++)
            {
                //blur V
                attachment.BlurMaterial.SetVector("BlurDir", BlurV);
                Graphics.Blit(volumLightBlurH, volumLightBlurV, attachment.BlurMaterial);

                //blur H
                attachment.BlurMaterial.SetVector("BlurDir", BlurH);
                Graphics.Blit(volumLightBlurV, volumLightBlurH, attachment.BlurMaterial);
            }
              
            //create volum light texture clone but with point filtering mode for upscaling algorithm
            RenderTexture volumLightPoint = RenderTexture.GetTemporary(volumLightBlurH.descriptor);
            volumLightPoint.filterMode = FilterMode.Point;
            Graphics.Blit(volumLightBlurH, volumLightPoint);

            //apply uniforms for upscaling
            attachment.CombineMaterial.SetTexture("LightScatteringTexturePoint", volumLightPoint);
            attachment.CombineMaterial.SetTexture("LightScatteringTextureLinear", volumLightBlurH);

            attachment.CombineMaterial.SetTexture("LowResolutionDepth", attachment.DownscaledDepthTarget);
            attachment.CombineMaterial.SetFloat("DepthThreshold", DepthThreshold);

            //combine scene texture with volum light (and in the same shader apply upscaling algorithm for volum light texture)
            Graphics.Blit(source, destination, attachment.CombineMaterial);

            RenderTexture.ReleaseTemporary(volumLightBlurV);
            RenderTexture.ReleaseTemporary(volumLightBlurH);
            RenderTexture.ReleaseTemporary(volumLightPoint);
        }
        else
        {
            if (EffectEnabled)
                Debug.LogError("Lack of some material or directional light attachment!");

            Graphics.Blit(source, destination);
        }
    }
}
