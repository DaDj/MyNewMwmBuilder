// Decompiled with JetBrains decompiler
// Type: MwmBuilder.MyMeshPartSolver
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using Assimp;
using System.Collections.Generic;
using System.IO;
using VRage;
using VRageMath;
using VRageRender.Import;

namespace MwmBuilder
{
    public class MyMeshPartSolver
    {
        public static string ColorMetalSuffix = "_cm.dds";
        public static string NormalGlossSuffix = "_ng.dds";
        public static string AddMapsSuffix = "_add.dds";
        public static string AlphamaskSuffix = "_alphamask.dds";
        private Dictionary<int, MyMeshPartInfo> m_partContainer = new Dictionary<int, MyMeshPartInfo>();

        public Dictionary<int, MyMeshPartInfo> GetMeshPartContainer() => m_partContainer;

        public void SetMaterial(Material material)
        {
            //if (material.GetMaterialTextureCount(TextureType.Diffuse) == 0)
            //    return;
            int hashCode = material.Name.GetHashCode();
            if (!m_partContainer.ContainsKey(hashCode))
                return;
            MyMeshPartInfo myMeshPartInfo = m_partContainer[hashCode];
            MyMaterialDescriptor matDesc = new MyMaterialDescriptor(material.Name);
            SetMaterialTextures(matDesc, material);
            if (myMeshPartInfo.m_MaterialDesc != null)
                matDesc.Technique = myMeshPartInfo.m_MaterialDesc.Technique;
            myMeshPartInfo.m_MaterialDesc = matDesc;
        }

        private void SetMaterialTextures(MyMaterialDescriptor matDesc, Material material)
        {
            TextureSlot texture;
            material.GetMaterialTexture(TextureType.Diffuse, 0, out texture);


            //Custom Change Nr 1.
            //Always set the path to this instead of filepath of textures.
            //This fixes the problem that assimp can't read pbr materials from max properly
            string filePath = material.Name + "_cm.dds";
            //string filePath = texture.FilePath;


            if (filePath.Length < MyMeshPartSolver.ColorMetalSuffix.Length)
                return;
            string str = filePath.Substring(0, filePath.Length - MyMeshPartSolver.ColorMetalSuffix.Length);
            try
            {
                string path2 = MyModelProcessor.GetResourcePathInContent(str + MyMeshPartSolver.ColorMetalSuffix).TrimStart('\\', '/');
               // if (File.Exists(Path.Combine(ProgramContext.OutputDir, path2)))
                    matDesc.Textures.Add("ColorMetalTexture", path2);
            }
            catch
            {
            }
            try
            {
                string path2 = MyModelProcessor.GetResourcePathInContent(str + MyMeshPartSolver.NormalGlossSuffix).TrimStart('\\', '/');
              //  if (File.Exists(Path.Combine(ProgramContext.OutputDir, path2)))
                    matDesc.Textures.Add("NormalGlossTexture", path2);
            }
            catch
            {
            }
            try
            {
                string path2 = MyModelProcessor.GetResourcePathInContent(str + MyMeshPartSolver.AddMapsSuffix).TrimStart('\\', '/');
                //if (File.Exists(Path.Combine(ProgramContext.OutputDir, path2)))
                    matDesc.Textures.Add("AddMapsTexture", path2);
            }
            catch
            {
            }
            try
            {
                string path2 = MyModelProcessor.GetResourcePathInContent(str + MyMeshPartSolver.AlphamaskSuffix).TrimStart('\\', '/');
                //if (!File.Exists(Path.Combine(ProgramContext.OutputDir, path2)))
                //    return;
                matDesc.Textures.Add("AlphamaskTexture", path2);
            }
            catch
            {
            }
        }

        public void SetIndices(
          Assimp.Mesh sourceMesh,
          VRageRender.Import.Mesh mesh,
          int[] indices,
          List<Vector3> vertices,
          int matHash)
        {
            MyDebug.Assert(indices.Length % 3 == 0, file: "E:\\Repo3\\Sources\\Tools\\MwmBuilder\\MyMeshPartSolver.cs", line: 126);
            MyMeshPartInfo myMeshPartInfo;
            if (!m_partContainer.TryGetValue(matHash, out myMeshPartInfo))
            {
                myMeshPartInfo = new MyMeshPartInfo();
                myMeshPartInfo.m_MaterialHash = matHash;
                m_partContainer.Add(matHash, myMeshPartInfo);
            }
            mesh.StartIndex = myMeshPartInfo.m_indices.Count;
            mesh.IndexCount = indices.Length;
            int vertexOffset = mesh.VertexOffset;
            for (int index = 0; index < sourceMesh.FaceCount * 3; index += 3)
            {
                int num1 = indices[index] + vertexOffset;
                int num2 = indices[index + 1] + vertexOffset;
                int num3 = indices[index + 2] + vertexOffset;
                myMeshPartInfo.m_indices.Add(num1);
                myMeshPartInfo.m_indices.Add(num2);
                myMeshPartInfo.m_indices.Add(num3);
            }
        }

        public void Clear() => m_partContainer.Clear();
    }
}
