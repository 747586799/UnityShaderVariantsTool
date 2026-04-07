#if UNITY_EDITOR
using System;
using ShaderVariantsEditor;
using UnityEditor;
using UnityEngine;

namespace AutoUploadShaderVariant
{
    [InitializeOnLoad]
    public class AutoUploadShaderVariantTool
    {
        static AutoUploadShaderVariantTool()
        {
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        }

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            switch (obj)
            {
                case PlayModeStateChange.EnteredEditMode: //停止播放事件监听后被监听
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    OnCheckShaderVariant();
                    break;
            }
        }

        private static void OnCheckShaderVariant()
        {
            try
            {
                // 从配置文件加载配置
                var config = ShaderVariantsHelper.LoadConfig();

                // 检查是否启用自动上传
                if (!config.enableAutoUpload)
                {
                    return;
                }

                EditorUtility.DisplayProgressBar("CheckShaderVariant", "CreateShaderVariant", 0.1f);
                if (EditorPrefs.HasKey("OnCheckShaderVariantLastTime"))
                {
                    long lastTime = long.Parse(EditorPrefs.GetString("OnCheckShaderVariantLastTime"));
                    long nowTime = ShaderVariantTimeHelper.ClientNowMillisecond();
                    if (lastTime + 10 * 60 * 1000 > nowTime)
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                }

                ShaderVariantsHelper.SaveCurrentShaderVariantCollection();
                AssetDatabase.Refresh();
                EditorUtility.DisplayProgressBar("CheckShaderVariant", "CheckHaveChange", 0.2f);
                bool isHaveChange = ShaderVariantsHelper.CheckHaveChange();
                if (!isHaveChange)
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }

                EditorUtility.DisplayProgressBar("CheckShaderVariant", "UploadToFTP", 0.3f);
                ShaderVariantsHelper.UploadTempShaderVariantsToFTP(
                    config.ftpServer,
                    config.ftpRemotePath,
                    config.ftpUsername,
                    config.ftpPassword);

                EditorUtility.DisplayProgressBar("CheckShaderVariant", "ClearCurrentShaderVariantCollection", 0.4f);
                EditorPrefs.SetString("OnCheckShaderVariantLastTime", ShaderVariantTimeHelper.ClientNowMillisecond().ToString());
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            finally
            {
                ShaderVariantsHelper.ClearCurrentShaderVariantCollection();
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
