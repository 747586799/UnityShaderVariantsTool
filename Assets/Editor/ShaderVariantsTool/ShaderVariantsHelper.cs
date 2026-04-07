#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ShaderVariantsEditor;

/// <summary>
/// ShaderVariants 工具类 - 提供 ShaderVariant 收集、合并、上传等功能
/// </summary>
public static class ShaderVariantsHelper
{
    #region 常量

    private const string TempShaderVariantsName = "TempShaderVariants";
    public const string TempShaderVariantsPath = "Assets/Editor/ShaderVariants/Temp/";
    public const string TempShaderVariantsFullPath = TempShaderVariantsPath + TempShaderVariantsName + ".shadervariants";
    public const string BaseShaderVariantsPath = "Assets/BundleRes/Shader/ShaderVariants.shadervariants";
    public const string ConfigPath = "Assets/Editor/ShaderVariants/ShaderVariantsEditorConfig.json";

    #endregion

    #region ShaderVariant 收集

    /// <summary>
    /// 保存当前 ShaderVariantCollection
    /// </summary>
    public static void SaveCurrentShaderVariantCollection()
    {
        ShaderVariantFileHelper.CheckPath(TempShaderVariantsPath, true);
        InvokeInternalStaticMethod(typeof(ShaderUtil), "SaveCurrentShaderVariantCollection", TempShaderVariantsFullPath);
    }

    /// <summary>
    /// 清理 Temp 文件
    /// </summary>
    public static void ClearCurrentShaderVariantCollection()
    {
        if (!Directory.Exists(TempShaderVariantsPath))
            return;

        Directory.Delete(TempShaderVariantsPath, true);

        string metaPath = TempShaderVariantsPath + ".meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 检查是否有变化
    /// </summary>
    public static bool CheckHaveChange()
    {
        ShaderVariantCollection baseCollection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(BaseShaderVariantsPath);
        ShaderVariantCollection newCollection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(TempShaderVariantsFullPath);

        if (baseCollection == null || newCollection == null)
            return true;

        ShaderVariantDatas baseData = new ShaderVariantDatas(baseCollection);
        return baseData.CheckIsSame(new ShaderVariantDatas(newCollection));
    }

    #endregion

    #region FTP 操作

    /// <summary>
    /// 上传 TempShaderVariants 到 FTP
    /// </summary>
    public static void UploadTempShaderVariantsToFTP(string ftpServer, string ftpRemotePath, string ftpUsername, string ftpPassword)
    {
        try
        {
            if (!File.Exists(TempShaderVariantsFullPath))
            {
                Debug.LogError("文件不存在：" + TempShaderVariantsFullPath);
                return;
            }

            string remoteFilePath = $"{ftpRemotePath.TrimEnd('/')}/{TempShaderVariantsName}_{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.shadervariants";
            string fullFtpUrl = ftpServer.TrimEnd('/') + "/" + remoteFilePath.TrimStart('/');

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(fullFtpUrl));
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
            request.UseBinary = true;
            request.KeepAlive = false;

            byte[] fileContents = File.ReadAllBytes(TempShaderVariantsFullPath);
            request.ContentLength = fileContents.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(fileContents, 0, fileContents.Length);
            }

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                Debug.Log($"上传成功，状态：{response.StatusDescription}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"上传失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 从 FTP 下载 ShaderVariants 文件，并修复对象名称
    /// </summary>
    /// <param name="ftpServer">FTP 服务器地址</param>
    /// <param name="ftpRemotePath">远程文件路径</param>
    /// <param name="ftpUsername">用户名</param>
    /// <param name="ftpPassword">密码</param>
    /// <param name="remoteFileName">远程文件名</param>
    /// <param name="savePath">保存路径</param>
    /// <returns>是否下载成功</returns>
    public static bool DownloadShaderVariantsFromFTP(string ftpServer, string ftpRemotePath, string ftpUsername, string ftpPassword, string remoteFileName, string savePath)
    {
        try
        {
            string fullFtpUrl = ftpServer.TrimEnd('/') + "/" + ftpRemotePath.TrimStart('/') + remoteFileName;

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(fullFtpUrl));
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
            request.UseBinary = true;

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            {
                ShaderVariantFileHelper.CheckPath(Path.GetDirectoryName(savePath), false);

                // 先下载到临时字符串
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string content = reader.ReadToEnd();

                    // 修复对象名称，将 m_Name: TempShaderVariants 替换为文件名
                    string expectedName = Path.GetFileNameWithoutExtension(savePath);
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"m_Name:\s*[^\r\n]+",
                        $"m_Name: {expectedName}");

                    // 写回文件
                    File.WriteAllText(savePath, content);
                }
            }

            Debug.Log($"下载成功：{remoteFileName}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"下载失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取 FTP 上的 ShaderVariants 文件列表
    /// </summary>
    public static string[] GetShaderVariantsFileList(string ftpServer, string ftpRemotePath, string ftpUsername, string ftpPassword)
    {
        try
        {
            string fullFtpUrl = ftpServer.TrimEnd('/') + "/" + ftpRemotePath.TrimStart('/');

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(fullFtpUrl));
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.Credentials = new NetworkCredential(ftpUsername, ftpPassword);

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                List<string> fileList = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.EndsWith(".shadervariants"))
                    {
                        fileList.Add(line);
                    }
                }
                return fileList.ToArray();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"获取文件列表失败：{ex.Message}");
            return new string[0];
        }
    }

    #endregion

    #region Merge ShaderVariants

    /// <summary>
    /// 合并 ShaderVariants
    /// </summary>
    public static void MergeShaderVariants(string basePath, string tempPath)
    {
        ShaderVariantCollection baseCollection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(basePath);
        List<string> paths = ShaderVariantFileHelper.GetAllFiles(TempShaderVariantsPath, file => !file.EndsWith(".meta"));

        ShaderVariantDatas baseData = new ShaderVariantDatas(baseCollection);

        foreach (var path in paths)
        {
            ShaderVariantCollection collection2 = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
            if (collection2 != null)
            {
                baseData.Merge(new ShaderVariantDatas(collection2));
            }
        }

        OptimizeShaderVariants(baseData);

        ShaderVariantCollection mergedCollection = new ShaderVariantCollection();
        foreach (var kvp in baseData.ShaderVariantDic)
        {
            foreach (var shaderVariant in kvp.Value)
            {
                mergedCollection.Add(shaderVariant);
            }
        }

        AssetDatabase.CreateAsset(mergedCollection, basePath);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 优化 ShaderVariants - 移除不需要的 Shader
    /// </summary>
    public static void OptimizeShaderVariants(ShaderVariantDatas shaderVariantDatas)
    {
        var dic = new Dictionary<Shader, List<ShaderVariantCollection.ShaderVariant>>(shaderVariantDatas.ShaderVariantDic);

        foreach (var kvp in dic)
        {
            string path = AssetDatabase.GetAssetPath(kvp.Key);

            // BundleRes 中的 Shader 保留
            if (path.StartsWith("Assets/BundleRes"))
                continue;

            // Editor 路径
            if (path.Contains("Editor"))
            {
                shaderVariantDatas.ShaderVariantDic.Remove(kvp.Key);
                Debug.Log($"移除 shader 变体，shader 名：{kvp.Key.name}，路径：{path}");
                continue;
            }

            // Debug/Editor 关键词
            if (kvp.Key.name.Contains("Debug") || kvp.Key.name.Contains("Editor"))
            {
                shaderVariantDatas.ShaderVariantDic.Remove(kvp.Key);
                Debug.Log($"移除 shader 变体，shader 名：{kvp.Key.name}，路径：{path}");
                continue;
            }

            // Sirenix
            if (kvp.Key.name.Contains("Sirenix"))
            {
                shaderVariantDatas.ShaderVariantDic.Remove(kvp.Key);
                Debug.Log($"移除 shader 变体，shader 名：{kvp.Key.name}，路径：{path}");
                continue;
            }

            // GUI
            if (kvp.Key.name.Contains("Internal-GUI"))
            {
                shaderVariantDatas.ShaderVariantDic.Remove(kvp.Key);
                Debug.Log($"移除 shader 变体，shader 名：{kvp.Key.name}，路径：{path}");
                continue;
            }

            // GraphView
            if (kvp.Key.name.Contains("GraphView"))
            {
                shaderVariantDatas.ShaderVariantDic.Remove(kvp.Key);
                Debug.Log($"移除 shader 变体，shader 名：{kvp.Key.name}，路径：{path}");
                continue;
            }

            // Terrain/Nature
            if (kvp.Key.name.Contains("Nature") || kvp.Key.name.Contains("TerrainEngine") || kvp.Key.name.Contains("Terrain"))
            {
                shaderVariantDatas.ShaderVariantDic.Remove(kvp.Key);
                Debug.Log($"移除 shader 变体，shader 名：{kvp.Key.name}，路径：{path}");
                continue;
            }

            // Skybox
            if (kvp.Key.name.Contains("Skybox"))
            {
                shaderVariantDatas.ShaderVariantDic.Remove(kvp.Key);
                Debug.Log($"移除 shader 变体，shader 名：{kvp.Key.name}，路径：{path}");
                continue;
            }

            // VR
            if (kvp.Key.name.Contains("Universal Render Pipeline/VR"))
            {
                shaderVariantDatas.ShaderVariantDic.Remove(kvp.Key);
                Debug.Log($"移除 shader 变体，shader 名：{kvp.Key.name}，路径：{path}");
                continue;
            }
        }
    }

    #endregion

    #region 自定义优化规则

    /// <summary>
    /// 应用自定义优化规则
    /// </summary>
    public static void ApplyCustomOptimizeRules(ShaderVariantDatas data,
        List<string> removeShaderNames, List<string> removePaths, List<string> keepPaths,
        Action<string, string> logCallback = null)
    {
        var dic = new Dictionary<Shader, List<ShaderVariantCollection.ShaderVariant>>(data.ShaderVariantDic);

        foreach (var kvp in dic)
        {
            string path = AssetDatabase.GetAssetPath(kvp.Key);
            string shaderName = kvp.Key.name;

            // 检查保留路径
            bool isKeepPath = keepPaths.Any(p => path.Contains(p)) || path.StartsWith("Assets/BundleRes");
            if (isKeepPath)
                continue;

            bool shouldRemove = false;
            string reason = "";

            // 检查自定义移除 Shader 名字
            foreach (var name in removeShaderNames)
            {
                if (shaderName.Contains(name))
                {
                    shouldRemove = true;
                    reason = $"自定义 Shader 名字：{name}";
                    break;
                }
            }

            // 检查自定义移除路径
            if (!shouldRemove)
            {
                foreach (var removePath in removePaths)
                {
                    if (path.Contains(removePath))
                    {
                        shouldRemove = true;
                        reason = $"自定义移除路径：{removePath}";
                        break;
                    }
                }
            }

            if (shouldRemove)
            {
                data.ShaderVariantDic.Remove(kvp.Key);
                logCallback?.Invoke(kvp.Key.name, reason);
            }
        }
    }

    #endregion

    #region 配置管理

    /// <summary>
    /// 加载配置
    /// </summary>
    public static ShaderVariantsEditorConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            return new ShaderVariantsEditorConfig();
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            return JsonUtility.FromJson<ShaderVariantsEditorConfig>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"加载配置失败：{e.Message}");
            return new ShaderVariantsEditorConfig();
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public static void SaveConfig(ShaderVariantsEditorConfig config)
    {
        try
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"保存配置失败：{e.Message}");
        }
    }

    #endregion

    #region 工具方法

    private static object InvokeInternalStaticMethod(Type type, string method, params object[] parameters)
    {
        var methodInfo = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
        if (methodInfo == null)
        {
            Debug.LogError($"{method} method didn't exist");
            return null;
        }

        return methodInfo.Invoke(null, parameters);
    }

    /// <summary>
    /// 加载 ShaderVariantCollection
    /// </summary>
    public static ShaderVariantCollection LoadShaderVariantCollection(string path)
    {
        return AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
    }

    /// <summary>
    /// 构建 Shader 分组数据
    /// </summary>
    public static List<ShaderGroupData> BuildShaderGroups(ShaderVariantDatas data)
    {
        var groups = new List<ShaderGroupData>();

        if (data?.ShaderVariantDic == null)
            return groups;

        foreach (var kvp in data.ShaderVariantDic)
        {
            groups.Add(new ShaderGroupData
            {
                Shader = kvp.Key,
                Path = AssetDatabase.GetAssetPath(kvp.Key),
                Variants = kvp.Value
            });
        }

        groups.Sort((a, b) => string.Compare(a.Shader.name, b.Shader.name, StringComparison.Ordinal));
        return groups;
    }

    /// <summary>
    /// 保存 ShaderVariantCollection
    /// </summary>
    public static void SaveShaderVariantCollection(string path, ShaderVariantDatas data)
    {
        ShaderVariantCollection collection = new ShaderVariantCollection();

        foreach (var kvp in data.ShaderVariantDic)
        {
            foreach (var variant in kvp.Value)
            {
                collection.Add(variant);
            }
        }

        // 设置资产名称与文件名匹配
        string fileName = Path.GetFileNameWithoutExtension(path);
        collection.name = fileName;

        AssetDatabase.CreateAsset(collection, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    #endregion
}

/// <summary>
/// ShaderVariants 编辑器配置
/// </summary>
[Serializable]
public class ShaderVariantsEditorConfig
{
    public string shaderVariantsPath = ShaderVariantsHelper.BaseShaderVariantsPath;
    public string[] customRemoveShaderNames = new string[0];
    public string[] customRemovePaths = new string[0];
    public string[] customKeepPaths = new string[0];

    // FTP 配置
    public string ftpServer = "ftp://192.168.1.16/";
    public string ftpRemotePath = "ShaderVariants/DiggingPlanet/";
    public string ftpUsername = "anonymous";
    public string ftpPassword = "";
    public bool enableAutoUpload = false;
}

/// <summary>
/// Shader 分组数据
/// </summary>
public class ShaderGroupData
{
    public Shader Shader;
    public string Path;
    public List<ShaderVariantCollection.ShaderVariant> Variants;
}
#endif
