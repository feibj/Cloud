using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEditor;
using System;

public class BakeVolumeCloud : MonoBehaviour
{
    public ComputeShader m_Compute;
    public Light m_Light;
    public Texture3D m_NoiseTexture;
    public Texture m_RBNoiseTexture;
    

    private static string s_RebuildCloud = "RebuildCloud";
    private static readonly int s_LightDirID = Shader.PropertyToID("_LightDir");
    private static readonly int s_Noise3DInputID = Shader.PropertyToID("_Noise3DInput");
    private static readonly int s_CloudBakeResultID = Shader.PropertyToID("CloudBakeResult");
    private static readonly int s_TextureSizeID = Shader.PropertyToID("_RebuildTextureSize");
    private readonly string m_SaveRootPath = @"Assets\Alian\baked";
    
    public Vector4 RebuiltTextureSize ;
    public String m_RebuiltTextureName = "RebuildNoise3D";

    [Button("重构3D噪声")]
    public void RebuildVolumeNoise()
    {
        if (m_NoiseTexture == null || m_Compute == null) return;
        int kernelIndex = FindKernel(s_RebuildCloud);
        if (kernelIndex == -1) return;
        int threadGroups = Mathf.CeilToInt(m_NoiseTexture.width*m_NoiseTexture.height*RebuiltTextureSize.z / 1024.0f);
        Vector4[] noise3DArray = new Vector4[(int)RebuiltTextureSize.x * (int)RebuiltTextureSize.y * (int)RebuiltTextureSize.z];
        ComputeBuffer noise3DResult = CreateBuffer(noise3DArray, sizeof(float) * 4);
        m_Compute.SetTexture(kernelIndex, s_Noise3DInputID, m_NoiseTexture);
        m_Compute.SetVector(s_TextureSizeID,RebuiltTextureSize);
        m_Compute.SetBuffer(kernelIndex, s_CloudBakeResultID, noise3DResult);
        m_Compute.Dispatch(kernelIndex, threadGroups, 1, 1);

        noise3DResult.GetData(noise3DArray);
        noise3DResult.Dispose();

        Color[] resultColors = new Color[noise3DArray.Length];
        for (int i = 0; i < resultColors.Length; i++)
        {
            resultColors[i] = noise3DArray[i];
        }
    
        string savePath = m_SaveRootPath + "\\"+m_RebuiltTextureName;
        if(Is_Bake2DTexture)
        {
            Texture2D tex2D = new Texture2D((int)RebuiltTextureSize.x ,(int)RebuiltTextureSize.y*(int)RebuiltTextureSize.z , TextureFormat.RGBA32, 0 ,true) ;
            tex2D.SetPixels(resultColors);
            tex2D.Apply();

            byte[] bytes = tex2D.EncodeToPNG();
            System.IO.File.WriteAllBytes(savePath+".tga", bytes);
        }
        else
        {
            Texture3D tex3D = new Texture3D((int)RebuiltTextureSize.x,(int)RebuiltTextureSize.y,(int)RebuiltTextureSize.z, TextureFormat.RGBA32, 0);
            tex3D.SetPixels(resultColors);
            tex3D.Apply();
            if (File.Exists(savePath))
            AssetDatabase.DeleteAsset(savePath);
            AssetDatabase.CreateAsset(tex3D, savePath+".Asset");
        }
        AssetDatabase.Refresh();
        noise3DResult.Release();
    }

    
    private static string s_BakeLightDirDensity = "BakeLightDirDensity";
    public String m_BakedTextureName = "BakedNoise3D";
    public bool Is_Bake2DTexture = false;

    [Button("Bake")]
    public void BakeVolumeNoise()
    {
        if (m_RBNoiseTexture == null || m_Compute == null) return;
        int kernelIndex = FindKernel(s_BakeLightDirDensity);
        if (kernelIndex == -1) return;
        Vector4 lightDir = new Vector4(0.0f,1.0f,0.0f,0.0f);
        if(m_Light!=null)
        {
            lightDir = m_Light.transform.forward;
        }
        
        int threadGroups = Mathf.CeilToInt(RebuiltTextureSize.x*RebuiltTextureSize.y*RebuiltTextureSize.z / 1024.0f);
        Vector4[] noise3DArray = new Vector4[(int)RebuiltTextureSize.x * (int)RebuiltTextureSize.y * (int)RebuiltTextureSize.z];
        ComputeBuffer noise3DResult = CreateBuffer(noise3DArray, sizeof(float) * 4);
        m_Compute.SetVector(s_LightDirID, lightDir);
        m_Compute.SetVector(s_TextureSizeID,RebuiltTextureSize);
        m_Compute.SetTexture(kernelIndex, s_Noise3DInputID, m_RBNoiseTexture);
        m_Compute.SetBuffer(kernelIndex, s_CloudBakeResultID, noise3DResult);
        m_Compute.Dispatch(kernelIndex, threadGroups, 1, 1);

        noise3DResult.GetData(noise3DArray);
        noise3DResult.Dispose();

        Color[] resultColors = new Color[noise3DArray.Length];
        for (int i = 0; i < resultColors.Length; i++)
        {
            resultColors[i] = noise3DArray[i];
        }

        string savePath = m_SaveRootPath + "\\" + m_BakedTextureName;
        if(Is_Bake2DTexture)
        {
            Texture2D tex2D = new Texture2D((int)RebuiltTextureSize.x ,(int)RebuiltTextureSize.y*(int)RebuiltTextureSize.z , TextureFormat.RGBA32, 0 ,true) ;
            tex2D.SetPixels(resultColors);
            tex2D.Apply();

            byte[] bytes = tex2D.EncodeToPNG();
            System.IO.File.WriteAllBytes(savePath+".tga", bytes);
        }
        else
        {
            Texture3D tex3D = new Texture3D((int)RebuiltTextureSize.x,(int)RebuiltTextureSize.y,(int)RebuiltTextureSize.z, TextureFormat.RGBA32, 0);
            tex3D.SetPixels(resultColors);
            tex3D.Apply();
            if (File.Exists(savePath))
            AssetDatabase.DeleteAsset(savePath);
            AssetDatabase.CreateAsset(tex3D, savePath+".Asset");
        }
    
        AssetDatabase.Refresh();
        noise3DResult.Release();
    }


    private int FindKernel(string kernelName)
    {
        if (m_Compute == null) return -1;
        if (m_Compute.HasKernel(kernelName))
            return m_Compute.FindKernel(kernelName);
        else
            return -1; 
    }

    private RenderTexture CreateRenderTexture3D(int resolution)
    {
        RenderTexture temp = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = resolution,
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            useMipMap = false,
        };
        temp.Create();
        return temp;
    }

    private ComputeBuffer CreateBuffer(System.Array data, int stride)
    {
        ComputeBuffer buffer = new ComputeBuffer(data.Length, stride, ComputeBufferType.Raw);
        buffer.SetData(data);
        return buffer;
    }
}
