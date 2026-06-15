// boss_voxelizer — instance-VAT 再生コンポーネント（ビルド対応・完全シリアライズ）。
// Importer がフィールドを埋めてプレファブ化する。実行時は AssetDatabase 不使用。
// Built-in パイプライン / GPU インスタンシング / 頂点テクスチャフェッチ。
using UnityEngine;

namespace BossVat
{
    [System.Serializable]
    public class VatGroup
    {
        public int slotStart;
        public int slotCount;
        public Material material;   // opaque / glass（Importer が生成）
    }

    [ExecuteAlways]
    [AddComponentMenu("BOSS/BOSS VAT Player")]
    public class BossVatPlayer : MonoBehaviour
    {
        [Header("再生")]
        [Tooltip("再生速度（1=等速）")] public float speed = 1f;
        [Tooltip("エディタ停止中もプレビュー再生する")] public bool playInEditor = true;
        [Tooltip("影を落とす")] public bool castShadows = true;

        [Header("データ（Importer が設定）")]
        public Mesh cubeMesh;
        public int slotCount;
        public int frameCount;
        public int paletteSide = 1;
        public int fps = 30;
        public VatGroup[] groups;

        MaterialPropertyBlock _props;
        Matrix4x4[] _matrices;

        void Update()
        {
            if (cubeMesh == null || groups == null || frameCount <= 0) return;
            if (!Application.isPlaying && !playInEditor) return;

            float t = (Application.isPlaying ? Time.time : (float)GetEditorTime()) * speed;
            int frame = Mathf.FloorToInt(t * fps) % frameCount;
            if (frame < 0) frame += frameCount;
            float frame01 = (frame + 0.5f) / frameCount;
            var vparams = new Vector4(slotCount, frameCount, frame01, paletteSide);

            if (_props == null) _props = new MaterialPropertyBlock();
            var shadow = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            var local2world = transform.localToWorldMatrix;

            foreach (var g in groups)
            {
                if (g == null || g.material == null || g.slotCount <= 0) continue;
                g.material.SetVector("_VatParams", vparams);
                int remaining = g.slotCount;
                int start = g.slotStart;
                while (remaining > 0)
                {
                    int batch = Mathf.Min(remaining, 1023);
                    if (_matrices == null || _matrices.Length < batch)
                        _matrices = new Matrix4x4[1023];
                    for (int i = 0; i < batch; i++) _matrices[i] = local2world;
                    _props.Clear();
                    _props.SetFloat("_SlotOffset", start);
                    Graphics.DrawMeshInstanced(cubeMesh, 0, g.material, _matrices, batch,
                        _props, shadow, true, gameObject.layer);
                    start += batch;
                    remaining -= batch;
                }
            }
        }

        double GetEditorTime()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.timeAsDouble;
#endif
        }

#if UNITY_EDITOR
        void OnEnable()
        {
            // エディタプレビューを毎フレーム更新するため再描画を要求
            UnityEditor.EditorApplication.update -= Repaint;
            UnityEditor.EditorApplication.update += Repaint;
        }
        void OnDisable()
        {
            UnityEditor.EditorApplication.update -= Repaint;
        }
        void Repaint()
        {
            if (!Application.isPlaying && playInEditor)
                UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
