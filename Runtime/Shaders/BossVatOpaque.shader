// boss_voxelizer — instance-VAT 再生シェーダー（Built-in / GPU instancing / 分岐なし）。
// 色は color VAT から直接読む（パレットテクスチャ間接参照を廃止）。アンリット表示。
Shader "BOSS/VatOpaque"
{
    Properties
    {
        _PosVAT ("Position VAT", 2D) = "black" {}
        _ScaleVAT ("Scale VAT", 2D) = "black" {}
        _RotVAT ("Rotation VAT", 2D) = "black" {}
        _ColorVAT ("Color VAT", 2D) = "white" {}
        _VatParams ("VAT Params", Vector) = (1,1,0,1)  // x=slotCount y=frameCount z=frame01 w=unused
        _SlotOffset ("Slot Offset", Float) = 0
        _PosPivot ("Pos Pivot", Vector) = (0,0,0,0)
        _PosScale ("Pos Scale", Vector) = (1,1,1,0)
        _GammaDecode ("Gamma Decode", Float) = 0
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

            sampler2D _PosVAT, _ScaleVAT, _RotVAT, _ColorVAT;
            float4 _VatParams;
            float4 _PosPivot, _PosScale;
            float _SlotOffset;
            float _GammaDecode;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; uint instanceID : SV_InstanceID; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 albedo : TEXCOORD0;
                float3 wnormal : TEXCOORD1;
                float  alive : TEXCOORD2;
            };

            float3 boss_quat_rotate(float4 q, float3 v)
            { return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v); }

            v2f vert(appdata v)
            {
                v2f o;
                float slot = _SlotOffset + (float)v.instanceID;
                float2 uv = float2((slot + 0.5) / _VatParams.x, _VatParams.z);
                float4 p = tex2Dlod(_PosVAT, float4(uv,0,0));
                float4 s = tex2Dlod(_ScaleVAT, float4(uv,0,0));
                float4 q = tex2Dlod(_RotVAT, float4(uv,0,0));
                float4 c = tex2Dlod(_ColorVAT, float4(uv,0,0));
                if (_GammaDecode > 0.5) {   // EXR 取り込みの pow(1/2.2) を打ち消す
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
                o.albedo = c.rgb;
                o.alive = alive;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(i.alive - 0.5);
                // アンリット: 焼き込んだ色をそのまま表示。シーンライティング非依存。
                // 立体感のため固定方向の弱いシェードのみ（最低 0.72 で常に見える）。
                float3 nrm = normalize(i.wnormal);
                float shade = 0.72 + 0.28 * saturate(dot(nrm, normalize(float3(0.3, 0.9, -0.25))));
                return fixed4(i.albedo * shade, 1.0);
            }
            ENDCG
        }
    }
}
