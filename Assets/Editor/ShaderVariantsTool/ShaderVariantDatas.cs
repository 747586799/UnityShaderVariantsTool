#if UNITY_EDITOR
using System.Collections.Generic;
using Framework.Utility.Utility;
using Helper;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ShaderVariantDatas
{
    public Dictionary<Shader, List<ShaderVariantCollection.ShaderVariant>> ShaderVariantDic;

    public ShaderVariantDatas(ShaderVariantCollection collection)
    {
        ShaderVariantDic = new();
        if (collection != null)
        {
            SerializedObject serializedObject = new SerializedObject(collection);
            SerializedProperty m_Shaders = serializedObject.FindProperty("m_Shaders");

            for (int i = 0; i < m_Shaders.arraySize; ++i)
            {
                SerializedProperty pair = m_Shaders.GetArrayElementAtIndex(i);
                SerializedProperty first = pair.FindPropertyRelative("first");
                SerializedProperty second = pair.FindPropertyRelative("second");
                Shader shader = first.objectReferenceValue as Shader;
                if (shader == null)
                    continue;
                ShaderVariantDic[shader] = new List<ShaderVariantCollection.ShaderVariant>();
                SerializedProperty variants = second.FindPropertyRelative("variants");
                for (var vi = 0; vi < variants.arraySize; ++vi)
                {
                    SerializedProperty variantInfo = variants.GetArrayElementAtIndex(vi);

                    ShaderVariantCollection.ShaderVariant variant = PropToVariantObject(shader, variantInfo);
                    ShaderVariantDic[shader].Add(variant);
                }
            }
        }
    }

    public void Merge(ShaderVariantDatas shaderVariantDatas)
    {
        foreach (var keyValuePair in shaderVariantDatas.ShaderVariantDic)
        {
            if (!ShaderVariantDic.TryGetValue(keyValuePair.Key, out List<ShaderVariantCollection.ShaderVariant> list))
            {
                ShaderVariantDic.Add(keyValuePair.Key, keyValuePair.Value.DeepCopyList());
                foreach (var shaderVariant in keyValuePair.Value)
                {
                    DebugMerage(shaderVariant);
                }
            }
            else
            {
                foreach (var shaderVariant in keyValuePair.Value)
                {
                    bool isHave = false;
                    foreach (var variant in list)
                    {
                        if (CheckShaderVariantIsSame(variant, shaderVariant))
                        {
                            isHave = true;
                            break;
                        }
                    }

                    if (!isHave && ShaderIsHaveKeyWorld(shaderVariant))
                    {
                        list.Add(shaderVariant);
                        DebugMerage(shaderVariant);
                    }
                }
            }
        }
    }

    public bool CheckIsSame(ShaderVariantDatas shaderVariantDatas)
    {
        foreach (var keyValuePair in shaderVariantDatas.ShaderVariantDic)
        {
            if (!ShaderVariantDic.TryGetValue(keyValuePair.Key, out List<ShaderVariantCollection.ShaderVariant> list))
            {
                return true;
            }
            else
            {
                foreach (var shaderVariant in keyValuePair.Value)
                {
                    bool isHave = false;
                    foreach (var variant in list)
                    {
                        if (CheckShaderVariantIsSame(variant, shaderVariant))
                        {
                            isHave = true;
                            break;
                        }
                    }

                    if (!isHave)
                        return true;
                }
            }
        }

        return false;
    }

    private bool ShaderIsHaveKeyWorld(ShaderVariantCollection.ShaderVariant shaderVariant)
    {
        foreach (var shaderVariantKeyword in shaderVariant.keywords)
        {
            bool isHave = false;
            foreach (var keywordSpaceKeyword in shaderVariant.shader.keywordSpace.keywords)
            {
                if (shaderVariantKeyword == keywordSpaceKeyword.name)
                {
                    isHave = true;
                    break;
                }
            }

            if (!isHave)
            {
                return false;
            }
        }

        return true;
    }

    private bool CheckShaderVariantIsSame(ShaderVariantCollection.ShaderVariant shaderVariant0, ShaderVariantCollection.ShaderVariant shaderVariant1)
    {
        if (shaderVariant0.shader.GetInstanceID() != shaderVariant1.shader.GetInstanceID())
            return false;
        if (shaderVariant0.passType != shaderVariant1.passType)
            return false;
        if (shaderVariant0.keywords.Length != shaderVariant1.keywords.Length)
            return false;
        foreach (var shaderVariant0Keyword in shaderVariant0.keywords)
        {
            bool isHave = false;
            foreach (var shaderVariant1Keyword in shaderVariant1.keywords)
            {
                if (shaderVariant1Keyword == shaderVariant0Keyword)
                {
                    isHave = true;
                    break;
                }
            }

            if (!isHave)
            {
                return false;
            }
        }

        return true;
    }

    //将SerializedProperty转化为ShaderVariant
    private ShaderVariantCollection.ShaderVariant PropToVariantObject(Shader shader, SerializedProperty variantInfo)
    {
        PassType passType = (PassType)variantInfo.FindPropertyRelative("passType").intValue;
        string keywords = variantInfo.FindPropertyRelative("keywords").stringValue;
        string[] keywordSet = keywords.Split(' ');
        keywordSet = (keywordSet.Length == 1 && keywordSet[0] == "") ? new string[0] : keywordSet;

        ShaderVariantCollection.ShaderVariant newVariant = new ShaderVariantCollection.ShaderVariant()
        {
            shader = shader,
            keywords = keywordSet,
            passType = passType
        };

        return newVariant;
    }

    private void DebugMerage(ShaderVariantCollection.ShaderVariant shaderVariant)
    {
        string keyWord = "";
        foreach (var shaderVariantKeyword in shaderVariant.keywords)
        {
            keyWord += $"{shaderVariantKeyword} ";
        }

        Debug.Log($"Shader: {shaderVariant.shader.name},PassType:{shaderVariant.passType}, keyWord: {keyWord}");
    }
}
#endif
