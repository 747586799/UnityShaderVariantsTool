#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 构建时 Shader 变体剔除处理器
/// 根据 ShaderVariants 文件中的变体列表，剔除不在列表中的变体，减少打包体积
/// </summary>
public class ShaderVariantsBuildPreprocessor : IPreprocessShaders
{
    private static HashSet<string> s_AllowedVariantKeys;
    private static HashSet<int> s_KnownShaderIds;
    private static StripShaderVariantsMode s_StripMode;
    private static bool s_Initialized;
    private static readonly object s_Lock = new object();

    /// <summary>
    /// 回调优先级，值越小越先执行
    /// </summary>
    public int callbackOrder => 0;

    /// <summary>
    /// 处理 Shader 变体
    /// </summary>
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        lock (s_Lock)
        {
            if (!s_Initialized)
            {
                Initialize();
                s_Initialized = true;
            }
        }

        // 如果未启用剔除，不做任何处理
        if (s_StripMode == StripShaderVariantsMode.None)
            return;

        // 如果白名单为空，不做任何处理
        if (s_AllowedVariantKeys == null || s_AllowedVariantKeys.Count == 0)
            return;

        int shaderId = shader.GetInstanceID();

        // StripKnown 模式：ShaderVariants 中没有该 Shader 的数据，不做处理
        if (s_StripMode == StripShaderVariantsMode.StripKnown && !s_KnownShaderIds.Contains(shaderId))
            return;

        int originalCount = data.Count;

        for (int i = data.Count - 1; i >= 0; i--)
        {
            if (!IsVariantAllowed(shaderId, data[i]))
            {
                data.RemoveAt(i);
            }
        }

        int strippedCount = originalCount - data.Count;
        if (data.Count > 0)
        {
            Debug.Log($"[ShaderVariantsBuildPreprocessor] 保留 Shader: {shader.name}, 变体 {data.Count}/{originalCount}, 剔除 {strippedCount}");
        }
        else
        {
            Debug.LogWarning($"[ShaderVariantsBuildPreprocessor] 完全剔除 Shader: {shader.name}, 变体 {originalCount} 全部不在白名单中");
        }
    }

    /// <summary>
    /// 初始化：读取配置并构建允许变体集合
    /// </summary>
    private static void Initialize()
    {
        string configPath = "Assets/Editor/ShaderVariants/ShaderVariantsEditorConfig.json";
        if (!File.Exists(configPath))
        {
            Debug.LogWarning("[ShaderVariantsBuildPreprocessor] 配置文件不存在，跳过变体剔除");
            return;
        }

        string json = File.ReadAllText(configPath);
        ShaderVariantsEditorConfig config = JsonUtility.FromJson<ShaderVariantsEditorConfig>(json);
        if (config == null)
        {
            Debug.LogWarning("[ShaderVariantsBuildPreprocessor] 配置解析失败，跳过变体剔除");
            return;
        }

        if (config.stripShaderVariantsMode == StripShaderVariantsMode.None)
        {
            Debug.Log("[ShaderVariantsBuildPreprocessor] 变体剔除未启用（None 模式）");
            return;
        }

        s_StripMode = config.stripShaderVariantsMode;

        if (string.IsNullOrEmpty(config.shaderVariantsPath))
        {
            Debug.LogWarning("[ShaderVariantsBuildPreprocessor] shaderVariantsPath 为空，跳过变体剔除");
            return;
        }

        ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(config.shaderVariantsPath);
        if (collection == null)
        {
            Debug.LogWarning($"[ShaderVariantsBuildPreprocessor] 无法加载 ShaderVariants 文件：{config.shaderVariantsPath}");
            return;
        }

        ShaderVariantDatas datas = new ShaderVariantDatas(collection);
        s_AllowedVariantKeys = new HashSet<string>();
        s_KnownShaderIds = new HashSet<int>();

        foreach (var kvp in datas.ShaderVariantDic)
        {
            int shaderId = kvp.Key.GetInstanceID();
            s_KnownShaderIds.Add(shaderId);
            foreach (var variant in kvp.Value)
            {
                string key = BuildVariantKey(shaderId, variant.keywords);
                s_AllowedVariantKeys.Add(key);
            }
        }

        Debug.Log($"[ShaderVariantsBuildPreprocessor] 变体剔除已启用（{s_StripMode}），白名单 Shader 数：{s_KnownShaderIds.Count}，变体数：{s_AllowedVariantKeys.Count}");
    }

    /// <summary>
    /// 判断某个变体是否在允许列表中
    /// </summary>
    private static bool IsVariantAllowed(int shaderId, ShaderCompilerData compilerData)
    {
        ShaderKeyword[] keywords = compilerData.shaderKeywordSet.GetShaderKeywords();
        string key = BuildVariantKey(shaderId, keywords);
        return s_AllowedVariantKeys.Contains(key);
    }

    /// <summary>
    /// 构建变体匹配 Key
    /// </summary>
    private static string BuildVariantKey(int shaderId, string[] keywords)
    {
        if (keywords == null || keywords.Length == 0)
            return $"{shaderId}:";

        System.Array.Sort(keywords, string.CompareOrdinal);
        return $"{shaderId}:{string.Join(" ", keywords)}";
    }

    /// <summary>
    /// 构建变体匹配 Key（重载，用于 ShaderKeyword[]）
    /// </summary>
    private static string BuildVariantKey(int shaderId, ShaderKeyword[] keywords)
    {
        if (keywords == null || keywords.Length == 0)
            return $"{shaderId}:";

        string[] keywordNames = new string[keywords.Length];
        for (int i = 0; i < keywords.Length; i++)
        {
            keywordNames[i] = keywords[i].name;
        }

        System.Array.Sort(keywordNames, string.CompareOrdinal);
        return $"{shaderId}:{string.Join(" ", keywordNames)}";
    }
}
#endif
