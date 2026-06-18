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
        [System.Serializable] public class Textures { public string position; public string scale; public string rotation; public string color; public string palette_index; }
        [System.Serializable] public class Meta
        {
            public int fps; public int frame_count; public int slot_count;
            public Group[] groups; public Palette palette;
            public float[] position_pivot; public float[] position_scale;
            public float[] object_scale;   // Blender オブジェクトのワールドスケール
            public Textures textures; public string cube_mesh;
        }

        // ---- 質感スタイル ----
        public enum VatStyle { Opaque, Frosted, Clear, Emissive }

        static string ShaderForStyle(VatStyle s)
        {
            switch (s)
            {
                case VatStyle.Frosted:  return "BOSS/VatFrosted";
                case VatStyle.Clear:    return "BOSS/VatClear";
                case VatStyle.Emissive: return "BOSS/VatEmissive";
                default:                return "BOSS/VatOpaque";
            }
        }

        static string StyleSuffix(VatStyle s) => s.ToString();

        // 既定は常に「不透明」。ガラス系(Frosted/Clear)は正面の平らな面が透けて
        // 縦縞に見えるため、ユーザーが Importer で明示的に選んだときだけ使う。
        static VatStyle DefaultStyleForKind(string kind)
            => VatStyle.Opaque;

        string _sourceFolder = "";
        string _outputFolder = "Assets/BossVat";
        string _prefabName = "";

        // 質感の編集用にロードした meta とグループ毎のスタイル
        Meta _loadedMeta;
        string _loadedFolder = "";
        VatStyle[] _groupStyles;

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

            // ③ マテリアル毎の質感スタイル
            EditorGUILayout.Space();
            RefreshMetaIfNeeded();
            if (_loadedMeta != null && _loadedMeta.groups != null && _loadedMeta.groups.Length > 0)
            {
                EditorGUILayout.LabelField("③ マテリアル毎の質感スタイル", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    for (int i = 0; i < _loadedMeta.groups.Length; i++)
                    {
                        var g = _loadedMeta.groups[i];
                        string label = "マテリアル " + g.material_id + "（" + g.slot_count + " スロット）";
                        _groupStyles[i] = (VatStyle)EditorGUILayout.EnumPopup(label, _groupStyles[i]);
                    }
                }
                EditorGUILayout.HelpBox("不透明=ソリッド / 曇りガラス=半透明フロスト / クリアガラス=透明 / 発光=エミッシブ。\nBuilt-in・URP は自動で選択されます。", MessageType.None);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!IsReady()))
            {
                if (GUILayout.Button("④ VAT プレファブを生成", GUILayout.Height(36)))
                {
                    var prefab = BuildPrefab(_sourceFolder, _outputFolder, _prefabName, _groupStyles);
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
            EditorGUILayout.HelpBox("生成後、プレファブをシーンに置くだけで再生されます（Built-in / URP 自動対応）。", MessageType.None);
        }

        bool IsReady()
        {
            return !string.IsNullOrEmpty(_sourceFolder)
                && File.Exists(Path.Combine(_sourceFolder, "boss_vat_meta.json"))
                && !string.IsNullOrEmpty(_outputFolder)
                && _outputFolder.StartsWith("Assets");
        }

        // ソースフォルダが変わったら meta を読み直し、グループ毎のスタイル既定値を用意する。
        void RefreshMetaIfNeeded()
        {
            if (string.IsNullOrEmpty(_sourceFolder)) { _loadedMeta = null; _loadedFolder = ""; return; }
            if (_sourceFolder == _loadedFolder && _loadedMeta != null) return;
            _loadedFolder = _sourceFolder;
            _loadedMeta = null; _groupStyles = null;
            string metaPath = Path.Combine(_sourceFolder, "boss_vat_meta.json");
            if (!File.Exists(metaPath)) return;
            try
            {
                _loadedMeta = JsonUtility.FromJson<Meta>(File.ReadAllText(metaPath));
                if (_loadedMeta != null && _loadedMeta.groups != null)
                {
                    _groupStyles = new VatStyle[_loadedMeta.groups.Length];
                    for (int i = 0; i < _loadedMeta.groups.Length; i++)
                        _groupStyles[i] = DefaultStyleForKind(_loadedMeta.groups[i].kind);
                }
            }
            catch { _loadedMeta = null; }
        }

        // 互換: スタイル未指定なら kind から既定スタイルを使う。
        public static GameObject BuildPrefab(string sourceFolder, string outputFolder, string prefabName)
            => BuildPrefab(sourceFolder, outputFolder, prefabName, null);

        // テスト/自動化からも呼べる静的 API。styles はグループ毎の質感（null で kind 既定）。
        public static GameObject BuildPrefab(string sourceFolder, string outputFolder, string prefabName, VatStyle[] styles)
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

                // パイプライン（Built-in / URP）を検出し、対応シェーダーを Assets にコピー。
                // URP のシェーダーは URP 未導入プロジェクトでコンパイルエラーになるため、
                // パッケージ内では Unity 無視フォルダ（~）に置き、合致する方だけ取り込む。
                EnsureShaders();

                // 色 VAT のファイル名（新: color / 旧: palette_index）
                string colorFile = !string.IsNullOrEmpty(meta.textures.color)
                    ? meta.textures.color : meta.textures.palette_index;

                // ---- ファイルをコピー ----
                string[] files = {
                    meta.textures.position, meta.textures.scale, meta.textures.rotation,
                    colorFile, meta.cube_mesh
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

                // ---- EXR 取り込み設定（4 VAT すべてデータとして）----
                foreach (var ex in new[] { meta.textures.position, meta.textures.scale,
                                           meta.textures.rotation, colorFile })
                    ConfigureExr(destDir + "/" + ex);
                AssetDatabase.Refresh();

                // ---- アセット読み込み ----
                var cubeMesh = AssetDatabase.LoadAssetAtPath<Mesh>(destDir + "/" + meta.cube_mesh);
                var posVAT = LoadTex(destDir + "/" + meta.textures.position);
                var sclVAT = LoadTex(destDir + "/" + meta.textures.scale);
                var rotVAT = LoadTex(destDir + "/" + meta.textures.rotation);
                var colorVAT = LoadTex(destDir + "/" + colorFile);

                var pivot = ToVec(meta.position_pivot);
                var scale = ToVec(meta.position_scale);

                // ---- BossVatPlayer 付き GameObject ----
                var go = new GameObject(baseName + "_VAT");
                // Blender と同じ大きさになるよう、オブジェクトのワールドスケールを適用
                // （VAT 位置はローカル座標で焼かれている。Mixamo 等は cm スケールなので重要）。
                Vector3 objScale = Vector3.one;
                if (meta.object_scale != null && meta.object_scale.Length == 3)
                    objScale = new Vector3(meta.object_scale[0], meta.object_scale[1], meta.object_scale[2]);
                else
                    objScale = ParseScaleFromMatrix(File.ReadAllText(metaPath));  // 古い VAT 互換
                if (objScale.x != 0 && objScale.y != 0 && objScale.z != 0)
                    go.transform.localScale = objScale;
                // 軸整列ボクセルを「真正面（グリッド軸ぴったり）」から見ると、列の隙間が
                // 一直線に見通せて縦縞に見える（Blender はグリッドが傾いて見えるため出ない）。
                // 既定で少しヨー回転させ、奥行きのレイヤーで隙間が埋まるようにする。
                // ※真正面で見たいときはシーンで回転を 0 に戻せる（その場合は縞が出る）。
                go.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
                var player = go.AddComponent<BossVatPlayer>();
                player.cubeMesh = cubeMesh;
                player.slotCount = meta.slot_count;
                player.frameCount = meta.frame_count;
                player.paletteSide = 1;   // 色は color VAT から直接読むため未使用
                player.fps = meta.fps > 0 ? meta.fps : 30;

                // 同じスタイルはマテリアルを共有（生成数を抑える）
                var styleMats = new System.Collections.Generic.Dictionary<VatStyle, Material>();
                var grps = new System.Collections.Generic.List<VatGroup>();
                for (int i = 0; i < meta.groups.Length; i++)
                {
                    var g = meta.groups[i];
                    VatStyle style = (styles != null && i < styles.Length)
                        ? styles[i] : DefaultStyleForKind(g.kind);
                    Material mat;
                    if (!styleMats.TryGetValue(style, out mat))
                    {
                        mat = MakeMaterial(destDir, baseName, StyleSuffix(style), ShaderForStyle(style),
                            style, posVAT, sclVAT, rotVAT, colorVAT, pivot, scale,
                            meta.slot_count, meta.frame_count);
                        styleMats[style] = mat;
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
            VatStyle style, Texture pos, Texture scl, Texture rot, Texture color,
            Vector4 pivot, Vector4 scale, int slotCount, int frameCount)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogError("Shader not found: " + shaderName); return null; }
            var m = new Material(sh) { enableInstancing = true };
            m.SetTexture("_PosVAT", pos); m.SetTexture("_ScaleVAT", scl);
            m.SetTexture("_RotVAT", rot); m.SetTexture("_ColorVAT", color);
            m.SetVector("_PosPivot", pivot); m.SetVector("_PosScale", scale);
            m.SetVector("_VatParams", new Vector4(slotCount, frameCount, 0.5f / Mathf.Max(1, frameCount), 1));
            // Gamma カラースペースのプロジェクトでは Unity が EXR を pow(1/2.2) で取り込むため、
            // シェーダー側で逆補正（pow 2.2）する。Linear プロジェクトでは不要。
            m.SetFloat("_GammaDecode", PlayerSettings.colorSpace == ColorSpace.Gamma ? 1f : 0f);
            // スタイル既定パラメータ（マテリアルで後から微調整可）
            switch (style)
            {
                case VatStyle.Frosted:
                    m.SetFloat("_MinShade", 0.78f); m.SetFloat("_Alpha", 0.45f);
                    m.SetFloat("_Fresnel", 0.9f); m.SetFloat("_FresnelPower", 2.5f);
                    m.renderQueue = 3000;
                    break;
                case VatStyle.Clear:
                    m.SetFloat("_MinShade", 0.85f); m.SetFloat("_Alpha", 0.12f);
                    m.SetFloat("_Fresnel", 1.4f); m.SetFloat("_FresnelPower", 3.5f);
                    m.renderQueue = 3000;
                    break;
                case VatStyle.Emissive:
                    m.SetFloat("_EmissionStrength", 2.2f);
                    break;
                default:
                    m.SetFloat("_MinShade", 0.72f);
                    break;
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + baseName + "_" + suffix + ".mat");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // 現在のレンダーパイプラインが URP かどうか。
        static bool IsURPActive()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            return rp != null && rp.GetType().FullName.Contains("Universal");
        }

        // パッケージ内の Builtin~ / URP~ から、現在のパイプラインに合うシェーダーを
        // Assets/BossVatShaders へコピーする（固定の共有フォルダ。名前衝突を避けるため1セットのみ）。
        static readonly string ShaderDestDir = "Assets/BossVatShaders";
        static void EnsureShaders()
        {
            bool urp = IsURPActive();
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BossVatImporterWindow).Assembly);
            if (pkg == null || string.IsNullOrEmpty(pkg.resolvedPath))
            {
                Debug.LogWarning("BOSS VAT: パッケージ情報を取得できませんでした（シェーダーコピーをスキップ）。");
                return;
            }
            string srcDir = Path.Combine(pkg.resolvedPath, "Runtime", urp ? "URP~" : "Builtin~");
            if (!Directory.Exists(srcDir))
            {
                Debug.LogError("BOSS VAT: シェーダーフォルダが見つかりません: " + srcDir);
                return;
            }
            EnsureFolder(ShaderDestDir);
            foreach (var f in Directory.GetFiles(srcDir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".shader" && ext != ".hlsl") continue;
                // パイプライン切替時に取り残しが出ないよう毎回上書きコピー。
                File.Copy(f, AbsPath(ShaderDestDir + "/" + Path.GetFileName(f)), true);
            }
            AssetDatabase.Refresh();
            Debug.Log("BOSS VAT: " + (urp ? "URP" : "Built-in") + " 用シェーダーを " + ShaderDestDir + " に配置しました。");
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
            // VAT 幅 = slot_count（最大 16384）。8192 だと 15000 等のスロットが
            // 縮小され全データがずれる（Blender と見た目が変わる）。物理上限まで上げる。
            ti.maxTextureSize = 16384;
            var ps = ti.GetDefaultPlatformTextureSettings();
            ps.format = TextureImporterFormat.RGBAFloat;
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            ps.maxTextureSize = 16384;
            ps.overridden = true;
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

        // 古い VAT 互換: object_scale が無い meta から object_matrix_world の
        // 3x3 列ベクトル長（=ワールドスケール）を手動抽出する。
        static Vector3 ParseScaleFromMatrix(string json)
        {
            try
            {
                int key = json.IndexOf("\"object_matrix_world\"");
                if (key < 0) return Vector3.one;
                int open = json.IndexOf('[', key);
                if (open < 0) return Vector3.one;
                // 括弧の対応で行列ブロック全体を切り出す
                int depth = 0, end = open;
                for (int i = open; i < json.Length; i++)
                {
                    if (json[i] == '[') depth++;
                    else if (json[i] == ']') { depth--; if (depth == 0) { end = i; break; } }
                }
                string block = json.Substring(open, end - open + 1);
                var nums = System.Text.RegularExpressions.Regex.Matches(block, "-?[0-9]*\\.?[0-9]+(?:[eE][-+]?[0-9]+)?");
                if (nums.Count < 16) return Vector3.one;
                float[] m = new float[16];
                for (int i = 0; i < 16; i++) m[i] = float.Parse(nums[i].Value, System.Globalization.CultureInfo.InvariantCulture);
                // m は行優先 4x4。列 j のスケール = sqrt(m[0][j]^2+m[1][j]^2+m[2][j]^2)
                float sx = Mathf.Sqrt(m[0]*m[0] + m[4]*m[4] + m[8]*m[8]);
                float sy = Mathf.Sqrt(m[1]*m[1] + m[5]*m[5] + m[9]*m[9]);
                float sz = Mathf.Sqrt(m[2]*m[2] + m[6]*m[6] + m[10]*m[10]);
                if (sx == 0 || sy == 0 || sz == 0) return Vector3.one;
                return new Vector3(sx, sy, sz);
            }
            catch { return Vector3.one; }
        }
    }
}
