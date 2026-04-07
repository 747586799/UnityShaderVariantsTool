#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShaderVariantsEditor
{
    /// <summary>
    /// ShaderVariants 编辑器专用的时间工具类
    /// </summary>
    public static class ShaderVariantTimeHelper
    {
        private const int MillisecondUnit = 10000;
        private static readonly DateTime EpochDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 获取客户端当前时间戳（毫秒）
        /// </summary>
        public static long ClientNowMillisecond()
        {
            return (DateTime.UtcNow.Ticks - EpochDateTime.Ticks) / MillisecondUnit;
        }
    }

    /// <summary>
    /// ShaderVariants 编辑器专用的文件工具类
    /// </summary>
    public static class ShaderVariantFileHelper
    {
        /// <summary>
        /// 获取指定路径下的所有文件
        /// </summary>
        /// <param name="dir">目录路径</param>
        /// <param name="predicate">过滤条件</param>
        /// <returns>文件路径列表</returns>
        public static List<string> GetAllFiles(string dir, Predicate<string> predicate = null)
        {
            List<string> files = new List<string>();
            if (!Directory.Exists(dir))
                return files;

            var fls = Directory.GetFiles(dir);
            foreach (var fl in fls)
            {
                if (predicate == null || predicate.Invoke(fl))
                {
                    files.Add(fl);
                }
            }

            var subDirs = Directory.GetDirectories(dir);
            foreach (var subDir in subDirs)
            {
                files.AddRange(GetAllFiles(subDir, predicate));
            }

            return files;
        }

        /// <summary>
        /// 检查路径是否存在，不存在则创建
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="isCreat">是否创建</param>
        public static void CheckPath(string path, bool isCreat = false)
        {
            if (!Directory.Exists(path))
            {
                if (isCreat)
                    Directory.CreateDirectory(path);
            }
        }
    }

    /// <summary>
    /// ShaderVariants 编辑器专用的 List 扩展方法
    /// </summary>
    public static class ShaderVariantListExtensions
    {
        /// <summary>
        /// List 深拷贝
        /// </summary>
        public static List<T> DeepCopyList<T>(this List<T> list)
        {
            var copy = new List<T>();
            if (list == null) return copy;
            for (var i = 0; i < list.Count; i++) copy.Add(list[i]);
            return copy;
        }
    }
}
#endif
