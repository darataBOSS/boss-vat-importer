// boss_voxelizer — instance-VAT glass（fresnel 擬似・不透明キュー・分岐なし）。
// 色は color VAT から直接読む。アンリット + fresnel。
Shader "BOSS/VatGlass"
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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "UnityCG.cginc"

            sampler2D _PosVAT, _ScaleVAT, _RotVAT, _ColorVAT;
            float4 _VatParams;
            float4 _PosPivot, _PosScale;
            float _SlotOffset;
            float _GammaDecode;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 albedo : TEXCOORD0;
                float3 wnormal : TEXCOORD1;
                float  alive : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
            };

            float3 boss_quat_rotate(float4 q, float3 v)
            { return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v); }

            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v);
                float slot = _SlotOffset + (float)unity_InstanceID;
                float2 uv = float2((slot + 0.5) / _VatParams.x, _VatParams.z);
                float4 p = tex2Dlod(_PosVAT, float4(uv,0,0));
                float4 s = tex2Dlod(_ScaleVAT, float4(uv,0,0));
                float4 q = tex2Dlod(_RotVAT, float4(uv,0,0));
                float4 c = tex2Dlod(_ColorVAT, float4(uv,0,0));
                if (_GammaDecode > 0.5) {
                    p.xyz = pow(max(p.xyz, 0.0), 2.2);
                    s.xyz = pow(max(s.xyz, 0.0), 2.2);
                    c.xyz = pow(max(c.xyz, 0.0), 2.2);
                }
                float alive = p.a;
                float3 wpos = p.xyz * _PosScale.xyz + _PosPivot.xyz;
                float3 scl = s.xyz * alive;
                float3 local = wpos + boss_quat_rotate(q, v.vertex.xyz * scl);
                o.pos = UnityObjectToClipPos(float4(local, 1.0));
                o.wnormal = UnityObjectToWorldNormal(boss_quat_rotate(q, v.normal));
                o.viewDir = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, float4(local,1)).xyz);
                o.albedo = c.rgb;
                o.alive = alive;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(i.alive - 0.5);
                float3 nrm = normalize(i.wnormal);
                float3 vd = normalize(i.viewDir);
                float shade = 0.72 + 0.28 * saturate(dot(nrm, normalize(float3(0.3, 0.9, -0.25))));
                float fres = pow(1.0 - saturate(dot(nrm, vd)), 3.0);
                return fixed4(i.albedo * shade + fres * 0.35, 1.0);
            }
            ENDCG
        }
    }
}
