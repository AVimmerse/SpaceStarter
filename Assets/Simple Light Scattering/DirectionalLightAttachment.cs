using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering; 

[RequireComponent(typeof(Light))]
public class DirectionalLightAttachment : MonoBehaviour
{
    public const string SHADOW_MAP_UNIFORM_NAME = "ShadowMap";
    public static DirectionalLightAttachment Attachment;

    private GameObject GOForcingDepthRendering;
    
    private bool Initialized = false;

    private CommandBuffer shadowMapCopyCB = null;
    private LightEvent shadowMapCopyLightEvent = LightEvent.AfterShadowMap;

    private CommandBuffer volumetricLightCB = null;
    private LightEvent volumetricLightEvent = LightEvent.BeforeScreenspaceMask;

    private CommandBuffer DownsampleDepthCB;

    private Camera cam;
    [HideInInspector] public RenderTexture VolumetricLightTarget;
    [HideInInspector] public RenderTexture DownscaledDepthTarget;

    [HideInInspector] public Material BlurMaterial;
    [HideInInspector] public Material DownscaleMaterial;
    [HideInInspector] public Material LSMaterial;
    [HideInInspector] public Material CombineMaterial;

    private Dictionary<string, Material> Materials;

    private Light dirLight;
    [HideInInspector] public Color DirectionalLightColor;

    private void OnEnable()
    {
        Attachment = this;

        //create material instances
        Materials = new Dictionary<string, Material>();
        BlurMaterial = CreateMaterial("Hidden/SimpleScattering/LightScatteringBlurShader");
        DownscaleMaterial = CreateMaterial("Hidden/SimpleScattering/LightScatteringDownscaleDepthShader");
        LSMaterial = CreateMaterial("Hidden/SimpleScattering/LightScatteringMainShader");
        CombineMaterial = CreateMaterial("Hidden/SimpleScattering/LightScatteringCombineShader");
    }
    
    private Material CreateMaterial(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if(shader == null)
        {
            Debug.LogError("(SimpleLightScattering) Shader with name: " + shaderName + " not found!, asset cannot work without this shader." +
                " Make sure shaders are included in your build (add them to Always Included Shaders in ProjectSettings/Graphics).");
            return null;
        }

        Material newMaterial = new Material(shader);
        newMaterial.hideFlags = HideFlags.HideAndDontSave;

        Materials.Add(shaderName, newMaterial);
        return newMaterial;
    }

    public void CreateRenderTargets(int Downsample)
    {
        DestroyRenderTargets();

        DownscaledDepthTarget = RenderTexture.GetTemporary(cam.pixelWidth / Downsample, cam.pixelHeight / Downsample, 8, RenderTextureFormat.RFloat);
        DownscaledDepthTarget.filterMode = FilterMode.Point;

        VolumetricLightTarget = RenderTexture.GetTemporary(cam.pixelWidth / Downsample, cam.pixelHeight / Downsample, 0, RenderTextureFormat.ARGB32);
    }

    private void DestroyRenderTargets()
    {
        if (DownscaledDepthTarget != null)
            RenderTexture.ReleaseTemporary(DownscaledDepthTarget);
        if (VolumetricLightTarget != null)
            RenderTexture.ReleaseTemporary(VolumetricLightTarget);
    }

    private void CreateDownsampledDepthCommandBuffer()
    {
        DownsampleDepthCB = new CommandBuffer();
        DownsampleDepthCB.name = "Downsampling depth for volumetric light";

        DownsampleDepthCB.Blit(null, DownscaledDepthTarget, DownscaleMaterial);

        //clear VolumetricLightTarget before rendering to it
        DownsampleDepthCB.SetRenderTarget(VolumetricLightTarget);
        DownsampleDepthCB.ClearRenderTarget(false, true, new Color(0, 0, 0, 1));

        //add this command buffer
        //forward & deffered rendering support, only one at time of command buffer will be rendered (AfterDepthTexture = Forward, BeforeLighting = Deffered)
        cam.AddCommandBuffer(CameraEvent.BeforeLighting, DownsampleDepthCB);
        cam.AddCommandBuffer(CameraEvent.AfterDepthTexture, DownsampleDepthCB);
    }

    // Use this for initialization
    public void InitRendering(int Downsample, Camera cama)
    { 
        cam = cama;
        dirLight = GetComponent<Light>();
 
        if (!dirLight)
        {
            Debug.LogError("Directional light not found, can't render volumetric light!");
            return;
        }

        CreateRenderTargets(Downsample);
        CreateDownsampledDepthCommandBuffer();

        //shadow map copy pass
        shadowMapCopyCB = new CommandBuffer();
        shadowMapCopyCB.name = "Shadowmap Copy";

        //this command buffers does only one thing, copies current active render texture (which is shadow map) into global texture called SHADOW_MAP_UNIFORM_NAME
        shadowMapCopyCB.SetShadowSamplingMode(BuiltinRenderTextureType.CurrentActive, ShadowSamplingMode.RawDepth);
        shadowMapCopyCB.SetGlobalTexture(SHADOW_MAP_UNIFORM_NAME, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive));

        //execute command buffer right after the shadowmap is renderered
        dirLight.AddCommandBuffer(shadowMapCopyLightEvent, shadowMapCopyCB);

        //volumetric Light pass (we have to do this before screenspace shadow mask is calculated because otherwise matrices in shaders will change and rendering results will be bad)
        volumetricLightCB = new CommandBuffer();
        volumetricLightCB.name = "Volumetric light calculation";

        volumetricLightCB.Blit(null, VolumetricLightTarget, LSMaterial);

        dirLight.AddCommandBuffer(volumetricLightEvent, volumetricLightCB);

        //create go with mesh renderer with 0,0,0 scale in front of camera to force unity to render depth texture all the time and remove bug
        //where when you look at sky volumetric light stops to render (because depth texture is not rendered)
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        obj.name = "Force Depth Rendering Object (Simple Light Scattering)";
        obj.transform.parent = cam.transform;
        obj.transform.localPosition = new Vector3(0, 0, 0);
        obj.transform.localScale = new Vector3(0, 0, 0);
        obj.transform.position += cam.transform.forward * 5;
        Destroy(obj.GetComponent<Collider>());
        GOForcingDepthRendering = obj;

        Initialized = true;
    }

    public void ChangeCamera(int Downsample, Camera cam)
    {
        CleanUp();
        InitRendering(Downsample, cam);
    }

    private void Update()
    {
        if (!Initialized)
            return;

        LSMaterial.SetTexture("LowResolutionDepth", DownscaledDepthTarget);
        LSMaterial.SetMatrix("InverseViewMatrix", cam.cameraToWorldMatrix);

        DirectionalLightColor = dirLight.color; 
    }

    private void CleanUp()
    {
        DestroyRenderTargets();

        Light light = GetComponent<Light>();
        if (!light)
        {
            Debug.LogError("Directional light not found!, it disappeared while script was running?");
            return;
        }

        light.RemoveCommandBuffer(volumetricLightEvent, volumetricLightCB);
        light.RemoveCommandBuffer(shadowMapCopyLightEvent, shadowMapCopyCB);

        if (cam != null)
        {
            cam.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, DownsampleDepthCB);
            cam.RemoveCommandBuffer(CameraEvent.BeforeLighting, DownsampleDepthCB);
        }

        if(GOForcingDepthRendering != null)
            Destroy(GOForcingDepthRendering);

        Initialized = false;
    }

    private void OnDisable()
    {
        //destroy materials
        foreach (Material m in Materials.Values)
        {
            if (m != null)
                Destroy(m);
        }
        Materials.Clear();

        CleanUp();
    }
}
