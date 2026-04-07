#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ShaderVariants 编辑器窗口
/// </summary>
public class ShaderVariantsEditorWindow : EditorWindow
{
    #region 数据

    private ShaderVariantCollection _sourceCollection;
    private ShaderVariantDatas _sourceData;
    private List<ShaderGroupData> _shaderGroups = new();
    private Dictionary<Shader, bool> _shaderFoldouts = new();
    private Dictionary<Shader, Dictionary<int, bool>> _variantFoldouts = new();
    private Dictionary<Shader, HashSet<int>> _selectedVariants = new();

    // 搜索过滤
    private string _shaderSearch = "";
    private string _keywordSearch = "";

    // 滚动位置
    private Vector2 _scrollPosition;
    private Vector2 _leftScrollPosition;
    private bool _showOptimizeOptions = true;

    // 配置数据
    private ShaderVariantsEditorConfig _config;

    // 优化配置
    private List<string> _customRemoveShaderNames = new();
    private List<string> _customRemovePaths = new();
    private List<string> _customKeepPaths = new();

    // 自定义移除输入
    private string _newRemoveShaderName = "";
    private string _newRemovePath = "";
    private string _newKeepPath = "";

    // 保存提示
    private bool _hasUnsavedChanges = false;

    // FTP 文件列表
    private string[] _ftpFileList;
    private int _selectedFtpFileIndex = 0;
    private string _downloadSavePath = "";

    #endregion

    #region 菜单与生命周期

    [MenuItem("Custom Editor/Shader Variants Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<ShaderVariantsEditorWindow>("Shader Variants Editor");
        window.minSize = new Vector2(1000, 600);
    }

    private void OnEnable()
    {
        _config = ShaderVariantsHelper.LoadConfig();
        LoadConfigFromConfig();
        LoadShaderVariants();
    }

    private void OnDisable()
    {
        SaveConfigToConfig();
        ShaderVariantsHelper.SaveConfig(_config);
    }

    #endregion

    #region 配置加载/保存

    private void LoadConfigFromConfig()
    {
        _customRemoveShaderNames = _config.customRemoveShaderNames?.ToList() ?? new List<string>();
        _customRemovePaths = _config.customRemovePaths?.ToList() ?? new List<string>();
        _customKeepPaths = _config.customKeepPaths?.ToList() ?? new List<string>();
    }

    private void SaveConfigToConfig()
    {
        _config.customRemoveShaderNames = _customRemoveShaderNames.ToArray();
        _config.customRemovePaths = _customRemovePaths.ToArray();
        _config.customKeepPaths = _customKeepPaths.ToArray();
    }

    #endregion

    #region 数据加载

    private void LoadShaderVariants()
    {
        _sourceCollection = ShaderVariantsHelper.LoadShaderVariantCollection(_config.shaderVariantsPath);
        if (_sourceCollection != null)
        {
            _sourceData = new ShaderVariantDatas(_sourceCollection);
            BuildShaderGroups();
        }
    }

    private void BuildShaderGroups()
    {
        _shaderGroups = ShaderVariantsHelper.BuildShaderGroups(_sourceData);

        _shaderFoldouts.Clear();
        _variantFoldouts.Clear();
        _selectedVariants.Clear();

        foreach (var group in _shaderGroups)
        {
            _shaderFoldouts[group.Shader] = false;
            _variantFoldouts[group.Shader] = new Dictionary<int, bool>();
            _selectedVariants[group.Shader] = new HashSet<int>();
        }
    }

    #endregion

    #region GUI

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        // 左侧配置面板（固定宽度）
        EditorGUILayout.BeginVertical(GUILayout.Width(400));
        DrawLeftPanel();
        EditorGUILayout.EndVertical();

        // 右侧 Shader 列表
        EditorGUILayout.BeginVertical();
        DrawRightPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        _leftScrollPosition = EditorGUILayout.BeginScrollView(_leftScrollPosition);

        GUILayout.Label("Shader Variants 编辑器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawFilePathPanel();
        EditorGUILayout.Space();

        DrawInfoPanel();
        EditorGUILayout.Space();

        DrawOptimizeOptions();
        EditorGUILayout.Space();

        DrawMergeShaderVariantsPanel();
        EditorGUILayout.Space();

        // 保存配置按钮 - 放在左侧底部
        if (GUILayout.Button("保存所有配置", GUILayout.Height(30)))
        {
            SaveConfigToConfig();
            ShaderVariantsHelper.SaveConfig(_config);
            EditorUtility.DisplayDialog("保存成功", $"已保存配置到：\n{ShaderVariantsHelper.ConfigPath}", "确定");
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();

        DrawSearchBar();
        EditorGUILayout.Space();

        DrawShaderList();

        EditorGUILayout.Space();

        DrawBottomButtons();

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region 左侧面板绘制

    private void DrawFilePathPanel()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("ShaderVariants 文件", EditorStyles.boldLabel);

        // 路径选择和加载
        EditorGUILayout.BeginHorizontal();
        _config.shaderVariantsPath = EditorGUILayout.TextField(_config.shaderVariantsPath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFilePanel("选择 ShaderVariants 文件", "Assets", "shadervariants");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    _config.shaderVariantsPath = "Assets" + path.Substring(Application.dataPath.Length);
                }
                else
                {
                    _config.shaderVariantsPath = path;
                }
                LoadShaderVariants();
            }
        }
        if (GUILayout.Button("加载", GUILayout.Width(50)))
        {
            LoadShaderVariants();
        }
        EditorGUILayout.EndHorizontal();

        if (_sourceCollection != null)
        {
            EditorGUILayout.HelpBox($"已加载：{Path.GetFileName(_config.shaderVariantsPath)}", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawInfoPanel()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("统计", EditorStyles.boldLabel);

        int totalShaders = _shaderGroups.Count;
        int totalVariants = _shaderGroups.Sum(g => g.Variants.Count);

        EditorGUILayout.LabelField($"Shader: {totalShaders}");
        EditorGUILayout.LabelField($"Variant: {totalVariants}");

        if (_hasUnsavedChanges)
        {
            EditorGUILayout.HelpBox("有未保存的更改", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawOptimizeOptions()
    {
        EditorGUILayout.BeginVertical("box");
        _showOptimizeOptions = EditorGUILayout.Foldout(_showOptimizeOptions, "优化选项", true);

        if (_showOptimizeOptions)
        {
            // 自定义移除 Shader 名字
            EditorGUILayout.LabelField("移除 Shader 名字", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _newRemoveShaderName = EditorGUILayout.TextField(_newRemoveShaderName);
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                if (!string.IsNullOrEmpty(_newRemoveShaderName) && !_customRemoveShaderNames.Contains(_newRemoveShaderName))
                {
                    _customRemoveShaderNames.Add(_newRemoveShaderName);
                    _newRemoveShaderName = "";
                    _hasUnsavedChanges = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical("box");
            foreach (var name in _customRemoveShaderNames)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(name);
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    _customRemoveShaderNames.Remove(name);
                    _hasUnsavedChanges = true;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 自定义移除路径
            EditorGUILayout.LabelField("移除路径", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _newRemovePath = EditorGUILayout.TextField(_newRemovePath);
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                if (!string.IsNullOrEmpty(_newRemovePath) && !_customRemovePaths.Contains(_newRemovePath))
                {
                    _customRemovePaths.Add(_newRemovePath);
                    _newRemovePath = "";
                    _hasUnsavedChanges = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical("box");
            foreach (var path in _customRemovePaths)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(path);
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    _customRemovePaths.Remove(path);
                    _hasUnsavedChanges = true;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 自定义保留路径
            EditorGUILayout.LabelField("保留路径", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _newKeepPath = EditorGUILayout.TextField(_newKeepPath);
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                if (!string.IsNullOrEmpty(_newKeepPath) && !_customKeepPaths.Contains(_newKeepPath))
                {
                    _customKeepPaths.Add(_newKeepPath);
                    _newKeepPath = "";
                    _hasUnsavedChanges = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical("box");
            foreach (var path in _customKeepPaths)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(path);
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    _customKeepPaths.Remove(path);
                    _hasUnsavedChanges = true;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("应用优化规则", GUILayout.Height(25)))
            {
                ApplyOptimizeRules();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMergeShaderVariantsPanel()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Merge ShaderVariants", EditorStyles.boldLabel);

        _config.enableAutoUpload = EditorGUILayout.Toggle("启用自动上传", _config.enableAutoUpload);

        EditorGUILayout.Space();

        // FTP 配置
        EditorGUILayout.LabelField("FTP 配置", EditorStyles.boldLabel);
        _config.ftpServer = EditorGUILayout.TextField("服务器", _config.ftpServer);
        _config.ftpRemotePath = EditorGUILayout.TextField("远程路径", _config.ftpRemotePath);
        _config.ftpUsername = EditorGUILayout.TextField("用户名", _config.ftpUsername);
        _config.ftpPassword = EditorGUILayout.PasswordField("密码", _config.ftpPassword);

        EditorGUILayout.Space();

        // 完整流程按钮
        if (GUILayout.Button("保存ShaderVariants并上传", GUILayout.Height(30)))
        {
            ExecuteFullProcess();
        }

        EditorGUILayout.Space();

        // 下载 ShaderVariants
        EditorGUILayout.LabelField("从服务器下载合并", EditorStyles.boldLabel);

        if (_ftpFileList != null && _ftpFileList.Length > 0)
        {
            EditorGUILayout.LabelField($"FTP 上的文件数：{_ftpFileList.Length}", EditorStyles.miniLabel);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新文件列表"))
        {
            RefreshFtpFileList();
        }
        if (GUILayout.Button("下载所有并合并"))
        {
            DownloadAndMerge();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _downloadSavePath = EditorGUILayout.TextField("临时下载路径", _downloadSavePath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFolderPanel("选择临时下载文件夹", "Assets/Editor/ShaderVariants", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    _downloadSavePath = "Assets" + path.Substring(Application.dataPath.Length);
                }
                else
                {
                    _downloadSavePath = path;
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "下载合并流程：\n" +
            "1. 刷新文件列表 - 获取 FTP 上所有 ShaderVariants 文件\n" +
            "2. 下载所有并合并 - 下载所有文件到临时目录，依次合并到当前配置，然后删除临时文件",
            MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region 右侧面板绘制

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("Shader:", GUILayout.Width(50));
        _shaderSearch = EditorGUILayout.TextField(_shaderSearch, GUILayout.Width(150));

        GUILayout.Label("关键词:", GUILayout.Width(50));
        _keywordSearch = EditorGUILayout.TextField(_keywordSearch, GUILayout.Width(150));

        if (GUILayout.Button("清除", GUILayout.Width(50)))
        {
            _shaderSearch = "";
            _keywordSearch = "";
            GUI.FocusControl(null);
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField($"显示：{_shaderGroups.Count} Shaders", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawShaderList()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        int visibleCount = 0;

        foreach (var group in _shaderGroups)
        {
            // 应用搜索过滤
            if (!string.IsNullOrEmpty(_shaderSearch) && !group.Shader.name.Contains(_shaderSearch) && !group.Path.Contains(_shaderSearch))
                continue;

            bool hasKeywordMatch = string.IsNullOrEmpty(_keywordSearch);
            if (!hasKeywordMatch)
            {
                foreach (var variant in group.Variants)
                {
                    if (variant.keywords.Any(k => k.Contains(_keywordSearch, StringComparison.OrdinalIgnoreCase)))
                    {
                        hasKeywordMatch = true;
                        break;
                    }
                }
            }

            if (!hasKeywordMatch)
                continue;

            visibleCount++;

            // Shader 行
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            if (!_shaderFoldouts.TryGetValue(group.Shader, out bool foldout))
                foldout = false;

            _shaderFoldouts[group.Shader] = EditorGUILayout.Foldout(foldout, "");

            // Shader 名字（可点击复制）
            if (GUILayout.Button(new GUIContent(group.Shader.name, "点击复制 Shader 名字"), EditorStyles.boldLabel, GUILayout.MinWidth(200)))
            {
                EditorGUIUtility.systemCopyBuffer = group.Shader.name;
                ShowNotification(new GUIContent($"已复制：{group.Shader.name}"));
            }

            // 路径（可点击复制）
            if (GUILayout.Button(new GUIContent(group.Path, "点击复制路径"), EditorStyles.miniLabel, GUILayout.MinWidth(150)))
            {
                EditorGUIUtility.systemCopyBuffer = group.Path;
                ShowNotification(new GUIContent($"已复制路径"));
            }

            EditorGUILayout.LabelField($"变体数：{group.Variants.Count}", GUILayout.Width(80));

            // 全选/取消全选按钮
            if (GUILayout.Button("全选", GUILayout.Width(50)))
            {
                for (int i = 0; i < group.Variants.Count; i++)
                {
                    _selectedVariants[group.Shader].Add(i);
                }
                _hasUnsavedChanges = true;
            }

            if (GUILayout.Button("清空", GUILayout.Width(50)))
            {
                _selectedVariants[group.Shader].Clear();
                _hasUnsavedChanges = true;
            }

            // 添加到移除名字规则
            if (GUILayout.Button("+ 名字", GUILayout.Width(50)))
            {
                if (!_customRemoveShaderNames.Contains(group.Shader.name))
                {
                    _customRemoveShaderNames.Add(group.Shader.name);
                    _hasUnsavedChanges = true;
                    ShowNotification(new GUIContent($"已添加到移除规则"));
                }
                else
                {
                    ShowNotification(new GUIContent("已在移除规则中"));
                }
            }

            // 添加到移除路径规则
            if (GUILayout.Button("+ 路径", GUILayout.Width(50)))
            {
                if (!_customRemovePaths.Contains(group.Path))
                {
                    _customRemovePaths.Add(group.Path);
                    _hasUnsavedChanges = true;
                    ShowNotification(new GUIContent($"已添加到移除规则"));
                }
                else
                {
                    ShowNotification(new GUIContent("已在移除规则中"));
                }
            }

            // 移除整个 Shader
            if (GUILayout.Button("移除此 Shader", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("确认删除", $"确定要删除 Shader '{group.Shader.name}' 的所有变体吗？", "确定", "取消"))
                {
                    for (int i = 0; i < group.Variants.Count; i++)
                    {
                        _selectedVariants[group.Shader].Add(i);
                    }
                    _hasUnsavedChanges = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Variant 列表
            if (foldout)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < group.Variants.Count; i++)
                {
                    var variant = group.Variants[i];

                    // 检查此变体是否匹配关键词搜索
                    if (!string.IsNullOrEmpty(_keywordSearch))
                    {
                        bool match = variant.keywords.Any(k => k.Contains(_keywordSearch, StringComparison.OrdinalIgnoreCase));
                        if (!match)
                            continue;
                    }

                    EditorGUILayout.BeginHorizontal();

                    bool isSelected = _selectedVariants[group.Shader].Contains(i);
                    bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                    if (newSelection != isSelected)
                    {
                        if (newSelection)
                            _selectedVariants[group.Shader].Add(i);
                        else
                            _selectedVariants[group.Shader].Remove(i);
                        _hasUnsavedChanges = true;
                    }

                    string passType = $"[{variant.passType}]";
                    string keywords = variant.keywords.Length > 0 ? string.Join(", ", variant.keywords) : "(无)";

                    EditorGUILayout.LabelField(passType, GUILayout.Width(60));
                    EditorGUILayout.LabelField(keywords, EditorStyles.wordWrappedMiniLabel);

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        if (visibleCount == 0)
        {
            EditorGUILayout.HelpBox("未找到匹配的 Shader", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawBottomButtons()
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(40));

        if (GUILayout.Button("刷新", GUILayout.Height(35), GUILayout.Width(80)))
        {
            LoadShaderVariants();
            _hasUnsavedChanges = false;
        }

        int selectedCount = _selectedVariants.Values.Sum(s => s.Count);
        EditorGUILayout.LabelField($"已选择：{selectedCount} variants", GUILayout.Width(120));

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("保存更改", GUILayout.Height(35), GUILayout.Width(120)))
        {
            SaveChanges();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("取消选择", GUILayout.Height(35), GUILayout.Width(80)))
        {
            foreach (var shader in _selectedVariants.Keys.ToList())
            {
                _selectedVariants[shader].Clear();
            }
            _hasUnsavedChanges = false;
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region 功能方法

    private void ApplyOptimizeRules()
    {
        int markedCount = 0;

        ShaderVariantsHelper.ApplyCustomOptimizeRules(
            _sourceData,
            _customRemoveShaderNames,
            _customRemovePaths,
            _customKeepPaths,
            (shaderName, reason) =>
            {
                // 找到对应的 Shader 并标记
                var group = _shaderGroups.FirstOrDefault(g => g.Shader.name == shaderName);
                if (group != null)
                {
                    for (int i = 0; i < group.Variants.Count; i++)
                    {
                        _selectedVariants[group.Shader].Add(i);
                    }
                    _shaderFoldouts[group.Shader] = true;
                }
                markedCount++;
                Debug.Log($"标记移除 Shader: {shaderName}, 原因：{reason}");
            });

        _hasUnsavedChanges = true;
        ShowNotification(new GUIContent($"已标记变体待删除，请点击保存按钮应用更改"));
    }

    private void SaveChanges()
    {
        if (_sourceCollection == null)
        {
            EditorUtility.DisplayDialog("错误", "未加载 ShaderVariants 文件", "确定");
            return;
        }

        // 统计要删除的数量
        int deleteCount = 0;
        foreach (var kvp in _selectedVariants)
        {
            deleteCount += kvp.Value.Count;
        }

        if (deleteCount == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有要删除的变体", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog("确认保存", $"确定要删除 {deleteCount} 个变体吗？\n\n此操作将直接修改 ShaderVariants 文件！", "确定", "取消"))
        {
            return;
        }

        // 创建新的 ShaderVariantCollection
        ShaderVariantCollection newCollection = new ShaderVariantCollection();

        foreach (var group in _shaderGroups)
        {
            for (int i = 0; i < group.Variants.Count; i++)
            {
                if (!_selectedVariants[group.Shader].Contains(i))
                {
                    newCollection.Add(group.Variants[i]);
                }
            }
        }

        // 替换原文件
        string path = _config.shaderVariantsPath;
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(newCollection, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"已保存 ShaderVariants，删除了 {deleteCount} 个变体，剩余 {newCollection.variantCount} 个变体");

        // 重新加载
        LoadShaderVariants();
        _hasUnsavedChanges = false;

        EditorUtility.DisplayDialog("完成", $"已成功保存，删除了 {deleteCount} 个变体", "确定");
    }

    private void UploadTempShaderVariantsToFTP()
    {
        ShaderVariantsHelper.UploadTempShaderVariantsToFTP(
            _config.ftpServer,
            _config.ftpRemotePath,
            _config.ftpUsername,
            _config.ftpPassword);
        EditorUtility.DisplayDialog("上传完成", "请查看 Console 确认上传结果", "确定");
    }

    private void CheckHaveChange()
    {
        bool isSame = ShaderVariantsHelper.CheckHaveChange();
        if (isSame)
        {
            EditorUtility.DisplayDialog("无变化", "当前 ShaderVariants 与 Temp 文件内容相同", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("有变化", "发现新的 Shader 变体，请执行 Merge 操作", "确定");
        }
    }

    private void MergeShaderVariants()
    {
        if (!File.Exists(ShaderVariantsHelper.TempShaderVariantsFullPath))
        {
            EditorUtility.DisplayDialog("错误", $"Temp 文件不存在，请先保存当前 Variant", "确定");
            return;
        }

        if (_sourceCollection == null)
        {
            EditorUtility.DisplayDialog("错误", $"请先加载 ShaderVariants 文件", "确定");
            return;
        }

        try
        {
            ShaderVariantsHelper.MergeShaderVariants(_config.shaderVariantsPath, ShaderVariantsHelper.TempShaderVariantsPath);

            EditorUtility.DisplayDialog(
                "Merge 完成",
                $"已合并 ShaderVariants:\n基础文件：{_config.shaderVariantsPath}\nTemp 文件：{ShaderVariantsHelper.TempShaderVariantsFullPath}",
                "确定");

            LoadShaderVariants();
        }
        catch (Exception e)
        {
            Debug.LogError($"Merge 失败：{e.Message}");
            EditorUtility.DisplayDialog("错误", $"Merge 失败：{e.Message}", "确定");
        }
    }

    /// <summary>
    /// 执行完整流程：保存 Variant → 检查变化 → 上传 FTP
    /// </summary>
    private void ExecuteFullProcess()
    {
        try
        {
            // 1. 保存当前 Variant
            EditorUtility.DisplayProgressBar("执行流程", "保存当前 ShaderVariant...", 0.33f);
            ShaderVariantsHelper.SaveCurrentShaderVariantCollection();
            AssetDatabase.Refresh();

            // 2. 检查是否有变化
            EditorUtility.DisplayProgressBar("执行流程", "检查变化...", 0.66f);
            bool isHaveChange = ShaderVariantsHelper.CheckHaveChange();
            if (!isHaveChange)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("提示", "ShaderVariants 无变化，无需更新", "确定");
                return;
            }

            // 3. 上传到 FTP
            EditorUtility.DisplayProgressBar("执行流程", "上传到 FTP...", 1.0f);
            ShaderVariantsHelper.UploadTempShaderVariantsToFTP(
                _config.ftpServer,
                _config.ftpRemotePath,
                _config.ftpUsername,
                _config.ftpPassword);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("完成", "上传成功！", "确定");
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"流程执行失败：{e.Message}");
            EditorUtility.DisplayDialog("错误", $"流程执行失败：{e.Message}", "确定");
        }
        finally
        {
            ShaderVariantsHelper.ClearCurrentShaderVariantCollection();
        }
    }

    /// <summary>
    /// 刷新 FTP 文件列表
    /// </summary>
    private void RefreshFtpFileList()
    {
        try
        {
            EditorUtility.DisplayProgressBar("FTP", "获取文件列表...", 0f);
            _ftpFileList = ShaderVariantsHelper.GetShaderVariantsFileList(
                _config.ftpServer,
                _config.ftpRemotePath,
                _config.ftpUsername,
                _config.ftpPassword);

            if (_ftpFileList.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "FTP 上没有找到 ShaderVariants 文件", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("成功", $"找到 {_ftpFileList.Length} 个 ShaderVariants 文件", "确定");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"获取文件列表失败：{e.Message}");
            EditorUtility.DisplayDialog("错误", $"获取文件列表失败：{e.Message}", "确定");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// 下载并合并 ShaderVariants - 下载所有文件到指定位置，依次合并后删除
    /// </summary>
    private void DownloadAndMerge()
    {
        if (_ftpFileList == null || _ftpFileList.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先刷新文件列表", "确定");
            return;
        }

        if (string.IsNullOrEmpty(_downloadSavePath))
        {
            EditorUtility.DisplayDialog("提示", "请选择保存路径", "确定");
            return;
        }

        if (_sourceCollection == null)
        {
            EditorUtility.DisplayDialog("错误", "请先加载基础 ShaderVariants 文件", "确定");
            return;
        }

        List<string> downloadedFiles = new List<string>();

        try
        {
            // 1. 下载所有文件
            for (int i = 0; i < _ftpFileList.Length; i++)
            {
                string fileName = _ftpFileList[i];
                string savePath = Path.Combine(Path.GetDirectoryName(_downloadSavePath), fileName);

                float progress = (float)i / _ftpFileList.Length * 0.5f;
                EditorUtility.DisplayProgressBar("下载", $"正在下载：{fileName} ({i + 1}/{_ftpFileList.Length})", progress);

                bool success = ShaderVariantsHelper.DownloadShaderVariantsFromFTP(
                    _config.ftpServer,
                    _config.ftpRemotePath,
                    _config.ftpUsername,
                    _config.ftpPassword,
                    fileName,
                    savePath);

                if (success)
                {
                    downloadedFiles.Add(savePath);
                }
                else
                {
                    Debug.LogWarning($"下载失败：{fileName}");
                }
            }

            if (downloadedFiles.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("错误", "没有成功下载任何文件", "确定");
                return;
            }

            AssetDatabase.Refresh();
            // 2. 依次合并所有下载的文件
            EditorUtility.DisplayProgressBar("合并", $"合并 ShaderVariants...", 0.6f);

            ShaderVariantDatas baseData = new ShaderVariantDatas(_sourceCollection);

            for (int i = 0; i < downloadedFiles.Count; i++)
            {
                string file = downloadedFiles[i];
                float progress = 0.6f + ((float)i / downloadedFiles.Count) * 0.3f;
                EditorUtility.DisplayProgressBar("合并", $"合并：{Path.GetFileName(file)} ({i + 1}/{downloadedFiles.Count})", progress);

                // 加载下载的文件
                ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(file);
                if (collection != null)
                {
                    // 修复对象名称与文件名匹配的问题
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    collection.name = fileName;

                    ShaderVariantDatas fileData = new ShaderVariantDatas(collection);
                    baseData.Merge(fileData);
                }
                else
                {
                    Debug.Log($"Not Find!!!");
                }
            }

            // 3. 保存合并后的结果
            EditorUtility.DisplayProgressBar("保存", "保存结果...", 0.95f);
            ShaderVariantsHelper.SaveShaderVariantCollection(_config.shaderVariantsPath, baseData);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(
                "完成",
                $"下载并合并成功！\n下载文件数：{downloadedFiles.Count}\n合并到：{_config.shaderVariantsPath}",
                "确定");

            LoadShaderVariants();
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"下载合并失败：{e.Message}");
            EditorUtility.DisplayDialog("错误", $"下载合并失败：{e.Message}", "确定");
        }
        finally
        {
            // 4. 删除所有下载的文件
            EditorUtility.DisplayProgressBar("清理", "删除临时文件...", 1f);
            foreach (string file in downloadedFiles)
            {
                try
                {
                    File.Delete(file);
                    string metaFile = file + ".meta";
                    if (File.Exists(metaFile))
                    {
                        File.Delete(metaFile);
                    }
                    Debug.Log($"已删除：{file}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"删除文件失败：{file}, {ex.Message}");
                }
            }
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }
    }

    #endregion
}
#endif
