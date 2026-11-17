using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable, VolumeComponentMenu("Sky/XPOCET - BOXOPHOBIC Day Night")]
[SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
[SkyUniqueID(947261)]
public sealed class XpocetCycleSky : SkySettings
{
    public const int UniqueId = 947261;
    public CubemapParameter dayTexture = new CubemapParameter(null);
    public CubemapParameter nightTexture = new CubemapParameter(null);
    public MaterialParameter rendererMaterial = new MaterialParameter(null);
    public ClampedFloatParameter daylight = new ClampedFloatParameter(1,0,1);
    public FloatParameter radiance = new FloatParameter(8000);
    public ColorParameter tint = new ColorParameter(Color.white, true, false, true);
    public Vector3Parameter sunDirection = new Vector3Parameter(Vector3.up);
    public FloatParameter sunVisibility = new FloatParameter(1);
    public FloatParameter moonVisibility = new FloatParameter(0);
    public FloatParameter rotationDegrees = new FloatParameter(0);
    public override Type GetSkyRendererType() => typeof(XpocetCycleSkyRenderer);
    public override int GetHashCode()
    {
        unchecked {
            int h=base.GetHashCode();
            h=h*23+dayTexture.GetHashCode(); h=h*23+nightTexture.GetHashCode();
            h=h*23+rendererMaterial.GetHashCode(); h=h*23+daylight.GetHashCode();
            h=h*23+radiance.GetHashCode(); h=h*23+tint.GetHashCode();
            h=h*23+sunDirection.GetHashCode(); h=h*23+rotationDegrees.GetHashCode();
            h=h*23+sunVisibility.GetHashCode(); h=h*23+moonVisibility.GetHashCode();
            return h;
        }
    }
}

public sealed class XpocetCycleSkyRenderer : SkyRenderer
{
    Material material, template;
    readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    public XpocetCycleSkyRenderer() { SupportDynamicSunLight=false; }
    public override void Build() { }
    public override void Cleanup() { CoreUtils.Destroy(material); material=null; template=null; }
    public override void RenderSky(BuiltinSkyParameters p, bool renderForCubemap, bool renderSunDisk)
    {
        var s=(XpocetCycleSky)p.skySettings;
        if(s.rendererMaterial.value==null || s.dayTexture.value==null || s.nightTexture.value==null) return;
        if(material==null || template!=s.rendererMaterial.value) {
            Cleanup(); template=s.rendererMaterial.value; material=CoreUtils.CreateEngineMaterial(template.shader);
        }
        block.Clear();
        block.SetMatrix("_PixelCoordToViewDirWS",p.pixelCoordToViewDirMatrix);
        block.SetTexture("_DayCube",s.dayTexture.value); block.SetTexture("_NightCube",s.nightTexture.value);
        block.SetVector("_Cycle",new Vector4(s.daylight.value,s.radiance.value,s.rotationDegrees.value*Mathf.Deg2Rad,renderSunDisk?1:0));
        block.SetColor("_SkyTint",s.tint.value);
        block.SetVector("_SunDirection",s.sunDirection.value);
        block.SetVector("_Disks",new Vector4(s.sunVisibility.value,s.moonVisibility.value,0,0));
        CoreUtils.DrawFullScreen(p.commandBuffer,material,block,renderForCubemap?0:1);
    }
}
