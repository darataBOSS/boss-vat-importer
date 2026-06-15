// boss_voxelizer — instance-VAT 再生シェーダー（Built-in / GPU instancing / 分岐なし）。
// 共通デコード: pos/scale/rot/palette を VAT(EXR) から頂点で取得し共有キューブを配置。
Shader "BOSS/VatOpaque"
{
    Properties
    {
        _PosVAT ("Position VAT", 2D) = "black" {}
        _ScaleVAT ("Scale VAT", 2D) = "black" {}
        _RotVAT ("Rotation VAT", 2D) = "black" {}
        _PalIdxVAT ("Palette Index VAT", 2D) = "black" {}
        _Palette ("Palette", 2D) = "white" {}
        _VatParams ("VAT Params", Vector) = (1,1,0,1)  // x=slotCount y=frameCount z=frame01 w=paletteSide
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
            #include "Lighting.cginc"

            sampler2D _PosVAT, _ScaleVAT, _RotVAT, _PalIdxVAT, _Palette;
            float4 _VatParams;
            float4 _PosPivot, _PosScale;
            float _SlotOffset;
            float _GammaDecode;   // Gamma カラースペース対策（Unity が EXR を pow(1/2.2) で取り込む）

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
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
                v2f o; UNITY_SETUP_INSTANCE_ID(v);
                float slot = _SlotOffset + (float)unity_InstanceID;
                float2 uv = float2((slot + 0.5) / _VatParams.x, _VatParams.z);
                float4 p = tex2Dlod(_PosVAT, float4(uv,0,0));
                float4 s = tex2Dlod(_ScaleVAT, float4(uv,0,0));
                float4 q = tex2Dlod(_RotVAT, float4(uv,0,0));
                float palIdx = tex2Dlod(_PalIdxVAT, float4(uv,0,0)).r;
                if (_GammaDecode > 0.5) {   // EXR 取り込みの pow(1/2.2) を打ち消す
                    p.xyz = pow(max(p.xyz, 0.0), 2.2);
                    s.xyz = pow(max(s.xyz, 0.0), 2.2);
                    palIdx = pow(max(palIdx, 0.0), 2.2);
                }
                float alive = p.a;
                float3 wpos = p.xyz * _PosScale.xyz + _PosPivot.xyz;
                float3 scl = s.xyz * alive;
                float3 local = wpos + boss_quat_rotate(q, v.vertex.xyz * scl);
                o.pos = UnityObjectToClipPos(float4(local,1.0));
                o.wnormal = UnityObjectToWorldNormal(boss_quat_rotate(q, v.normal));
                float side = _VatParams.w;
                float2 puv = float2((fmod(palIdx, side)+0.5)/side, (floor(palIdx/side)+0.5)/side);
                o.albedo = tex2Dlod(_Palette, float4(puv,0,0)).rgb;
                o.alive = alive;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(i.alive - 0.5);
                // アンリット: 焼き込んだ色(albedo)をそのまま表示。シーンのライティングに
                // 依存しない（Blender の見た目に近い）。立体感のため固定方向の弱いシェードのみ。
                float3 nrm = normalize(i.wnormal);
                float shade = 0.72 + 0.28 * saturate(dot(nrm, normalize(float3(0.3, 0.9, -0.25))));
                float3 col = i.albedo * shade;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
