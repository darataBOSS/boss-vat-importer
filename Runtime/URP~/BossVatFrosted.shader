// boss_voxelizer — instance-VAT 曇りガラス（フロスト・半透明・URP）。
Shader "BOSS/VatFrosted"
{
    Properties
    {
        _PosVAT ("Position VAT", 2D) = "black" {}
        _ScaleVAT ("Scale VAT", 2D) = "black" {}
        _RotVAT ("Rotation VAT", 2D) = "black" {}
        _ColorVAT ("Color VAT", 2D) = "white" {}
        _VatParams ("VAT Params", Vector) = (1,1,0,1)
        _SlotOffset ("Slot Offset", Float) = 0
        _PosPivot ("Pos Pivot", Vector) = (0,0,0,0)
        _PosScale ("Pos Scale", Vector) = (1,1,1,0)
        _GammaDecode ("Gamma Decode", Float) = 0
        _MinShade ("Min Shade", Range(0,1)) = 0.78
        _Alpha ("Base Alpha", Range(0,1)) = 0.45
        _Fresnel ("Fresnel Strength", Range(0,2)) = 0.9
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5
            #define BOSSVAT_URP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BossVatCore.hlsl"
            struct Attributes { float4 posOS : POSITION; float3 nrmOS : NORMAL; uint iid : SV_InstanceID; };
            struct Varyings { float4 pos : SV_POSITION; float3 color : TEXCOORD0; float3 wn : TEXCOORD1; float alive : TEXCOORD2; float3 vd : TEXCOORD3; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                BossVatVertex b = BossVat_Compute(v.posOS.xyz, v.nrmOS, v.iid);
                o.pos = BOSSVAT_OBJ2CLIP(b.localPos);
                o.color = b.color; o.wn = b.worldNormal; o.alive = b.alive;
                o.vd = normalize(BOSSVAT_CAMPOS - b.worldPos);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                clip(i.alive - 0.5);
                float3 n = normalize(i.wn);
                float fres = pow(1.0 - saturate(dot(n, normalize(i.vd))), _FresnelPower);
                float3 col = i.color * BossVat_Shade(i.wn) + fres * _Fresnel * 0.5;
                float a = saturate(_Alpha + fres * _Fresnel * (1.0 - _Alpha));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
