// boss_voxelizer — instance-VAT 共通頂点ロジック（Built-in / URP 両対応）。
// シェーダー側で URP のときだけ BOSSVAT_URP を define してから include する。
// パイプライン差（テクスチャサンプル・座標変換）はマクロで吸収する。
#ifndef BOSSVAT_CORE_INCLUDED
#define BOSSVAT_CORE_INCLUDED

#if defined(BOSSVAT_URP)
    #define BOSSVAT_TEX2D(n)        TEXTURE2D(n); SAMPLER(sampler##n)
    #define BOSSVAT_TEXLOAD(n, uv)  SAMPLE_TEXTURE2D_LOD(n, sampler##n, uv, 0)
    #define BOSSVAT_OBJ2CLIP(p)     TransformObjectToHClip(p)
    #define BOSSVAT_OBJ2WNORM(nrm)  TransformObjectToWorldNormal(nrm)
    #define BOSSVAT_OBJ2WPOS(p)     TransformObjectToWorld(p)
    #define BOSSVAT_CAMPOS          GetCameraPositionWS()
#else
    #define BOSSVAT_TEX2D(n)        sampler2D n
    #define BOSSVAT_TEXLOAD(n, uv)  tex2Dlod(n, float4(uv, 0, 0))
    #define BOSSVAT_OBJ2CLIP(p)     UnityObjectToClipPos(p)
    #define BOSSVAT_OBJ2WNORM(nrm)  UnityObjectToWorldNormal(nrm)
    #define BOSSVAT_OBJ2WPOS(p)     mul(unity_ObjectToWorld, float4(p, 1.0)).xyz
    #define BOSSVAT_CAMPOS          _WorldSpaceCameraPos.xyz
#endif

BOSSVAT_TEX2D(_PosVAT);
BOSSVAT_TEX2D(_ScaleVAT);
BOSSVAT_TEX2D(_RotVAT);
BOSSVAT_TEX2D(_ColorVAT);

float4 _VatParams;     // x=slotCount y=frameCount z=frame01 w=unused
float4 _PosPivot;
float4 _PosScale;
float  _SlotOffset;
float  _GammaDecode;
// スタイル共通パラメータ（使わないシェーダーでも宣言してOK）
float  _MinShade;
float  _Alpha;
float  _Fresnel;
float  _FresnelPower;
float  _EmissionStrength;

float3 BossVat_QuatRotate(float4 q, float3 v)
{
    return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
}

struct BossVatVertex
{
    float3 localPos;
    float3 worldNormal;
    float3 worldPos;
    float3 color;
    float  alive;
};

// 1 インスタンス = 1 キューブ。VAT から位置/スケール/回転/色を引いて頂点を変形する。
BossVatVertex BossVat_Compute(float3 vertexOS, float3 normalOS, uint instanceID)
{
    BossVatVertex o;
    float slot = _SlotOffset + (float)instanceID;
    float2 uv = float2((slot + 0.5) / _VatParams.x, _VatParams.z);
    float4 p = BOSSVAT_TEXLOAD(_PosVAT, uv);
    float4 s = BOSSVAT_TEXLOAD(_ScaleVAT, uv);
    float4 q = BOSSVAT_TEXLOAD(_RotVAT, uv);
    float4 c = BOSSVAT_TEXLOAD(_ColorVAT, uv);
    if (_GammaDecode > 0.5)   // Gamma プロジェクトの EXR 取り込み pow(1/2.2) を打ち消す
    {
        p.xyz = pow(max(p.xyz, 0.0), 2.2);
        s.xyz = pow(max(s.xyz, 0.0), 2.2);
        c.xyz = pow(max(c.xyz, 0.0), 2.2);
    }
    o.alive = p.a;
    float3 wpos = p.xyz * _PosScale.xyz + _PosPivot.xyz;
    float3 scl = s.xyz * o.alive;
    o.localPos = wpos + BossVat_QuatRotate(q, vertexOS * scl);
    o.worldNormal = BOSSVAT_OBJ2WNORM(BossVat_QuatRotate(q, normalOS));
    o.worldPos = BOSSVAT_OBJ2WPOS(o.localPos);
    o.color = c.rgb;
    return o;
}

// 固定方向の弱いシェード（最低 _MinShade で常に見える・シーンライティング非依存）。
float BossVat_Shade(float3 worldNormal)
{
    float ms = (_MinShade > 0.0) ? _MinShade : 0.72;
    float3 n = normalize(worldNormal);
    return ms + (1.0 - ms) * saturate(dot(n, normalize(float3(0.3, 0.9, -0.25))));
}

#endif // BOSSVAT_CORE_INCLUDED
