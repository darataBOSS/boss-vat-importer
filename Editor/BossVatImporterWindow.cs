// boss_voxelizer — VAT インポータ（Editor）。
// Blender が書き出した VAT フォルダを指定 → 取り込み・EXR 設定・マテリアル生成・
// 再生プレファブ生成までを自動化する。メニュー: BOSS > VAT Importer。
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BossVat.EditorTools
{
    public class BossVatImporterWindow : EditorWindow
    {
        // ---- meta JSON データクラス ----
        [System.Serializable] public class Group { public int material_id; public int slot_start; public int slot_count; public string kind; }
        [System.Serializable] public class Palette { public string file; public int colors; public int side; }
        [System.Serializable] public class Textures { public string position; public string scale; public string rotation; public string palette_index; }
        [System.Serializable] public class Meta
        {
            public int fps; public int frame_count; public int slot_count;
            public Group[] groups; public Palette palette;
            public float[] position_pivot; public float[] position_scale;
            public Textures textures; public string cube_mesh;
        }

        string _sourceFolder = "";
        string _outputFolder = "Assets/BossVat";
        string _prefabName = "";

        [MenuItem("BOSS/VAT Importer")]
        public static void Open()
        {
            var w = GetWindow<BossVatImporterWindow>("BOSS VAT Importer");
            w.minSize = new Vector2(420, 240);
            w._sourceFolder = EditorPrefs.GetString("BossVat.src", "");
            w._outputFolder = EditorPrefs.GetString("BossVat.out", "Assets/BossVat");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Blender の VAT フォルダから再生プレファブを生成", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 1) ソースフォルダ
            EditorGUILayout.LabelField("① 書き出しフォルダ（boss_vat_meta.json がある場所）");
            using (new EditorGUILayout.HorizontalScope())
            {
                _sourceFolder = EditorGUILayout.TextField(_sourceFolder);
                if (GUILayout.Button("参照", GUILayout.Width(60)))
                {
                    var p = EditorUtility.OpenFolderPanel("VAT 書き出しフォルダを選択", _sourceFolder, "");
                    if (!string.IsNullOrEmpty(p)) { _sourceFolder = p; EditorPrefs.SetString("BossVat.src", p); }
                }
            }

            // 2) 出力先（Assets 内）
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("② 出力先フォルダ（Assets 内）とプレファブ名");
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField(_outputFolder);
                if (GUILayout.Button("参照", GUILayout.Width(60)))
                {
                    var p = EditorUtility.OpenFolderPanel("出力先（Assets 内）", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(p))
                    {
                        if (p.StartsWith(Application.dataPath))
                            _outputFolder = "Assets" + p.Substring(Application.dataPath.Length);
                        else EditorUtility.DisplayDialog("BOSS VAT", "出力先は Assets 内を指定してください。", "OK");
                        EditorPrefs.SetString("BossVat.out", _outputFolder);
                    }
                }
            }
            _prefabName = EditorGUILayout.TextField("プレファブ名（空なら自動）", _prefabName);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!IsReady()))
            {
                if (GUILayout.Button("③ VAT プレファブを生成", GUILayout.Height(36)))
                {
                    var prefab = BuildPrefab(_sourceFolder, _outputFolder, _prefabName);
                    if (prefab != null)
                    {
                        EditorGUIUtility.PingObject(prefab);
                        Selection.activeObject = prefab;
                        EditorUtility.DisplayDialog("BOSS VAT",
                            "プレファブを生成しました:\n" + AssetDatabase.GetAssetPath(prefab) +
                            "\n\nシーンにドラッグするだけで再生されます。", "OK");
                    }
                }
            }
            if (!IsReady())
                EditorGUILayout.HelpBox("boss_vat_meta.json があるフォルダと、Assets 内の出力先を指定してください。", MessageType.Info);
            EditorGUILayout.HelpBox("生成後、プレファブをシーンに置くだけで再生されます（Built-in パイプライン）。", MessageType.None);
        }

        bool IsReady()
        {
            return !string.IsNullOrEmpty(_sourceFolder)
                && File.Exists(Path.Combine(_sourceFolder, "boss_vat_meta.json"))
                && !string.IsNullOrEmpty(_outputFolder)
                && _outputFolder.StartsWith("Assets");
        }

        // テスト/自動化からも呼べる静的 API。失敗時は null。
        public static GameObject BuildPrefab(string sourceFolder, string outputFolder, string prefabName)
        {
            try
            {
                string metaPath = Path.Combine(sourceFolder, "boss_vat_meta.json");
                var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(metaPath));
                if (meta == null || meta.textures == null)
                {
                    Debug.LogError("BOSS VAT: meta JSON を読めませんでした。");
                    return null;
                }

                string baseName = string.IsNullOrEmpty(prefabName)
                    ? SafeName(new DirectoryInfo(sourceFolder).Name) : SafeName(prefabName);
                if (baseName == "vat" || baseName == "")  // 出力名が紛らわしい時は親フォルダ名
                    baseName = SafeName(Directory.GetParent(sourceFolder).Name);
                string _outputFolder = outputFolder;
                string _sourceFolder = sourceFolder;

                string destDir = (_outputFolder.TrimEnd('/') + "/" + baseName);
                EnsureFolder(destDir);

                // ---- ファイルをコピー ----
                string[] files = {
                    meta.textures.position, meta.textures.scale, meta.textures.rotation,
                    meta.textures.palette_index, meta.palette.file, meta.cube_mesh
                };
                foreach (var f in files)
                {
                    if (string.IsNullOrEmpty(f)) continue;
                    string src = Path.Combine(_sourceFolder, f);
                    if (File.Exists(src))
                        File.Copy(src, AbsPath(destDir + "/" + f), true);
                }
                File.Copy(metaPath, AbsPath(destDir + "/boss_vat_meta.json"), true);
                AssetDatabase.Refresh();

                // ---- EXR 取り込み設定 ----
                foreach (var ex in new[] { meta.textures.position, meta.textures.scale,
                                           meta.textures.rotation, meta.textures.palette_index })
                    ConfigureExr(destDir + "/" + ex);
                ConfigurePalette(destDir + "/" + meta.palette.file);
                AssetDatabase.Refresh();

                // ---- アセット読み込み ----
                var cubeMesh = AssetDatabase.LoadAssetAtPath<Mesh>(destDir + "/" + meta.cube_mesh);
                var posVAT = LoadTex(destDir + "/" + meta.textures.position);
                var sclVAT = LoadTex(destDir + "/" + meta.textures.scale);
                var rotVAT = LoadTex(destDir + "/" + meta.textures.rotation);
                var palIdx = LoadTex(destDir + "/" + meta.textures.palette_index);
                var palette = LoadTex(destDir + "/" + meta.palette.file);

                var pivot = ToVec(meta.position_pivot);
                var scale = ToVec(meta.position_scale);
                int side = meta.palette != null ? meta.palette.side : 1;

                Material opaqueMat = null, glassMat = null;

                // ---- BossVatPlayer 付き GameObject ----
                var go = new GameObject(baseName + "_VAT");
                var player = go.AddComponent<BossVatPlayer>();
                player.cubeMesh = cubeMesh;
                player.slotCount = meta.slot_count;
                player.frameCount = meta.frame_count;
                player.paletteSide = side;
                player.fps = meta.fps > 0 ? meta.fps : 30;

                var grps = new System.Collections.Generic.List<VatGroup>();
                foreach (var g in meta.groups)
                {
                    bool glass = g.kind == "glass";
                    Material mat;
                    if (glass)
                    {
                        if (glassMat == null)
                            glassMat = MakeMaterial(destDir, baseName, "Glass", "BOSS/VatGlass",
                                posVAT, sclVAT, rotVAT, palIdx, palette, pivot, scale,
                                meta.slot_count, meta.frame_count, side);
                        mat = glassMat;
                    }
                    else
                    {
                        if (opaqueMat == null)
                            opaqueMat = MakeMaterial(destDir, baseName, "Opaque", "BOSS/VatOpaque",
                                posVAT, sclVAT, rotVAT, palIdx, palette, pivot, scale,
                                meta.slot_count, meta.frame_count, side);
                        mat = opaqueMat;
                    }
                    grps.Add(new VatGroup { slotStart = g.slot_start, slotCount = g.slot_count, material = mat });
                }
                player.groups = grps.ToArray();

                // ---- プレファブ保存 ----
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(destDir + "/" + baseName + ".prefab");
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                DestroyImmediate(go);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("BOSS VAT: プレファブを生成しました " + prefabPath);
                return prefab;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        // ---- helpers ----
        static Material MakeMaterial(string dir, string baseName, string suffix, string shaderName,
            Texture pos, Texture scl, Texture rot, Texture palIdx, Texture palette,
            Vector4 pivot, Vector4 scale, int slotCount, int frameCount, int side)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogError("Shader not found: " + shaderName); return null; }
            var m = new Material(sh) { enableInstancing = true };
            m.SetTexture("_PosVAT", pos); m.SetTexture("_ScaleVAT", scl);
            m.SetTexture("_RotVAT", rot); m.SetTexture("_PalIdxVAT", palIdx);
            m.SetTexture("_Palette", palette);
            m.SetVector("_PosPivot", pivot); m.SetVector("_PosScale", scale);
            m.SetVector("_VatParams", new Vector4(slotCount, frameCount, 0.5f / Mathf.Max(1, frameCount), side));
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + baseName + "_" + suffix + ".mat");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Texture2D LoadTex(string assetPath) => AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

        static Vector4 ToVec(float[] a)
            => (a != null && a.Length >= 3) ? new Vector4(a[0], a[1], a[2], 0) : Vector4.zero;

        static void ConfigureExr(string assetPath)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 8192;
            var ps = ti.GetDefaultPlatformTextureSettings();
            ps.format = TextureImporterFormat.RGBAFloat;
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SetPlatformTextureSettings(ps);
            ti.SaveAndReimport();
        }

        static void ConfigurePalette(string assetPath)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace("\\", "/");
            string leaf = Path.GetFileName(assetFolder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static string AbsPath(string assetPath)
            => Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));

        static string SafeName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(" ", "_");
        }
    }
}
