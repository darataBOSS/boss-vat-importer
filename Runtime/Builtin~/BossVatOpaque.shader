// boss_voxelizer — instance-VAT 不透明（Built-in パイプライン）。
// Importer がパイプライン検出して Assets にコピーする版。アンリット。
Shader "BOSS/VatOpaque"
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
        _MinShade ("Min Shade", Range(0,1)) = 0.72
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "UnityCG.cginc"
            #include "BossVatCore.hlsl"
            struct Attributes { float4 posOS : POSITION; float3 nrmOS : NORMAL; uint iid : SV_InstanceID; };
            struct Varyings { float4 pos : SV_POSITION; float3 color : TEXCOORD0; float3 wn : TEXCOORD1; float alive : TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                BossVatVertex b = BossVat_Compute(v.posOS.xyz, v.nrmOS, v.iid);
                o.pos = BOSSVAT_OBJ2CLIP(b.localPos);
                o.color = b.color; o.wn = b.worldNormal; o.alive = b.alive;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                clip(i.alive - 0.5);
                return fixed4(i.color * BossVat_Shade(i.wn), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
