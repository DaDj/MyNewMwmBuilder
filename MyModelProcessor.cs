// Decompiled with JetBrains decompiler
// Type: MwmBuilder.MyModelProcessor
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using Assimp;
using BulletXNA.BulletCollision;
using BulletXNA.LinearMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VRage;
using VRage.Import;
using VRage.Utils;
using VRageMath;
using VRageMath.PackedVector;
using VRageRender.Animations;
using VRageRender.Import;

namespace MwmBuilder
{
  public class MyModelProcessor : IPrimitiveManagerBase
  {
    private const string SECTION_NAME_PATTERN = "^(?<prefix>.+)_section_?(?<suffix>.*)$";
    private static Matrix Identity3DSMax = Matrix.CreateWorld(Vector3.Zero, Vector3.Up, -Vector3.Forward);
    private static readonly string C_MINER_WARS_MODEL_FORMAT_EXT = ".mwm";
    public static string ContentRootName = "Content";
    private bool m_containsTexChannel0 = true;
    private bool m_containsTexChannel1 = true;
    private Dictionary<string, MyModelDummy> m_dummies = new Dictionary<string, MyModelDummy>();
    private static string[] KnownMaterialProperties = new string[1]
    {
      "Technique"
    };
    private List<string> m_debug = new List<string>();
    private List<VRageRender.Import.Mesh> m_meshes = new List<VRageRender.Import.Mesh>();
    private List<MyMeshSectionInfo> m_sections = new List<MyMeshSectionInfo>();
    private List<Vector3> m_vertices = new List<Vector3>();
    private Dictionary<int, int> m_sourceMeshMap = new Dictionary<int, int>();
    private List<HalfVector4> m_packedVertices = new List<HalfVector4>();
    private List<Vector3> m_normals = new List<Vector3>();
    private List<Vector2> m_texCoords0 = new List<Vector2>();
    private List<Vector2> m_texCoords1 = new List<Vector2>();
    private List<Vector3> m_binormals = new List<Vector3>();
    private List<Vector3> m_tangents = new List<Vector3>();
    private List<int> m_bvhIndices = new List<int>();
    private List<NodeDesc> m_nodes = new List<NodeDesc>();
    private List<Vector3I> m_boneMapping = new List<Vector3I>();
    private List<Vector4I> m_blendIndices = new List<Vector4I>();
    private List<Vector4> m_blendWeights = new List<Vector4>();
    private Dictionary<string, MyAnimationClip> clips = new Dictionary<string, MyAnimationClip>();
    private Dictionary<string, int> m_nodeToIndex = new Dictionary<string, int>();
    private BoundingBox m_boundingBox;
    private BoundingSphere m_boundingSphere;
    private float m_patternScale = 1f;
    private MyMeshPartSolver m_MeshPartSolver = new MyMeshPartSolver();
    private ModelAnimations m_modelAnimations = new ModelAnimations();
    private readonly Dictionary<string, VRageRender.Import.Bone> m_bonesByName = new Dictionary<string, VRageRender.Import.Bone>();
    private readonly Dictionary<string, int> m_bonesToIndex = new Dictionary<string, int>();
    private List<Dictionary<int, List<VertexWeight>>> vertToBoneWeight = new List<Dictionary<int, List<VertexWeight>>>();
    private readonly List<VRageRender.Import.Bone> m_bones = new List<VRageRender.Import.Bone>();
    private Dictionary<string, Dictionary<string, object>> m_materialProperties = new Dictionary<string, Dictionary<string, object>>();
    private List<Tuple<float, Vector3I>> m_boneOffsetHelper = new List<Tuple<float, Vector3I>>();
    private List<Tuple<float, int>> m_boneIndexHelper = new List<Tuple<float, int>>();
    private const int BonesPerAxis = 3;

    public bool SwapWindingOrder { get; set; }

    public Dictionary<string, Dictionary<string, object>> MaterialProperties => this.m_materialProperties;

    public MyModelProcessor()
    {
      this.RescaleFactor = 1f;
      this.SkipAssimpPivots = false;
    }

    [Browsable(true)]
    [Description("Rescale factor. If RescaleToLengthInMeters = false, then each vertex is multiplied by this number. Model aspect ratio will remain.")]
    public float RescaleFactor { get; set; }

    [Browsable(true)]
    [DefaultValue(false)]
    [Description("If true then model uses additional channel textures. If false, model does not use channel texture.")]
    public bool UseChannelTextures { get; set; }

    [Browsable(true)]
    [DefaultValue(1)]
    [Description("Scale of armor pattern")]
    public float PatternScale
    {
      get => this.m_patternScale;
      set => this.m_patternScale = value;
    }

    [Browsable(true)]
    public float RotationX { get; set; }

    [Browsable(true)]
    public float RotationY { get; set; }

    [Browsable(true)]
    public float RotationZ { get; set; }

    [Browsable(true)]
    [DefaultValue(false)]
    [Description("If true then model uses only position data.")]
    public bool SimpleModel { get; set; }

    [Browsable(true)]
    public float? BoneGridMapping { get; set; }

    [Browsable(true)]
    public bool SkipAssimpPivots { get; set; }

    public Vector3[] BoneMapping { get; set; }

    public byte[] HavokCollisionShapes { get; set; }

    public MyLODDescriptor[] LODs { get; set; }

    protected void ConvertMaterial(
      Material material,
      string outputDir,
      IMyBuildLogger logger,
      string filename)
    {
      this.m_MeshPartSolver.SetMaterial(material);
      int hashCode = material.Name.GetHashCode();
      MyMeshPartInfo myMeshPartInfo = this.m_MeshPartSolver.GetMeshPartContainer()[hashCode];
      Dictionary<string, object> source = (Dictionary<string, object>) null;
      if (!this.MaterialProperties.TryGetValue(material.Name, out source))
      {
        Dictionary<string, object> dictionary = new Dictionary<string, object>();
        this.MaterialProperties.Add(material.Name, dictionary);
        dictionary["Technique"] = (object) myMeshPartInfo.m_MaterialDesc.Technique.ToString();
        foreach (KeyValuePair<string, string> texture in myMeshPartInfo.m_MaterialDesc.Textures)
          dictionary[texture.Key] = (object) texture.Value;
      }
      else
      {
        myMeshPartInfo.m_MaterialDesc.Textures = source.Where<KeyValuePair<string, object>>((Func<KeyValuePair<string, object>, bool>) (x => x.Key.Contains("Texture"))).ToDictionary<KeyValuePair<string, object>, string, string>((Func<KeyValuePair<string, object>, string>) (x => x.Key), (Func<KeyValuePair<string, object>, string>) (x => (string) x.Value));
        myMeshPartInfo.m_MaterialDesc.UserData = source.Where<KeyValuePair<string, object>>((Func<KeyValuePair<string, object>, bool>) (x => !x.Key.Contains("Texture") && !MyModelProcessor.KnownMaterialProperties.Contains<string>(x.Key))).ToDictionary<KeyValuePair<string, object>, string, string>((Func<KeyValuePair<string, object>, string>) (x => x.Key), (Func<KeyValuePair<string, object>, string>) (x => (string) x.Value));
        if (source.ContainsKey("Technique"))
        {
          string str = (string) source["Technique"];
          myMeshPartInfo.m_MaterialDesc.Technique = !string.IsNullOrEmpty(str) ? str : "MESH";
        }
        if (source.ContainsKey("GlassMaterialCW"))
          myMeshPartInfo.m_MaterialDesc.GlassCW = (string) source["GlassMaterialCW"];
        if (source.ContainsKey("GlassMaterialCCW"))
          myMeshPartInfo.m_MaterialDesc.GlassCCW = (string) source["GlassMaterialCCW"];
        if (source.ContainsKey("GlassSmooth"))
          myMeshPartInfo.m_MaterialDesc.GlassSmoothNormals = Convert.ToBoolean(source["GlassSmooth"]);
      }
      foreach (KeyValuePair<string, string> texture in myMeshPartInfo.m_MaterialDesc.Textures)
      {
        if (!string.IsNullOrEmpty(texture.Value))
        {
          if (Path.GetFileName(outputDir) != MyModelProcessor.ContentRootName)
          {
            int num = outputDir.LastIndexOf(MyModelProcessor.ContentRootName);
            if (num != -1)
              outputDir = outputDir.Remove(num + MyModelProcessor.ContentRootName.Length);
          }
          if (!File.Exists(Path.Combine(outputDir, texture.Value)))
            logger.LogMessage(MessageType.Error, "Texture " + texture.Value + " does not exist!", filename);
        }
      }
    }

    private void CheckTextureChannels(Scene input)
    {
      for (int index = 0; index < this.m_meshes.Count; ++index)
      {
        Assimp.Mesh mesh = input.Meshes[this.m_meshes[index].MeshIndex];
        if (mesh.TextureCoordinateChannels[0].Count == 0)
          this.m_containsTexChannel0 = false;
        if (mesh.TextureCoordinateChannels[1].Count == 0)
          this.m_containsTexChannel1 = false;
      }
    }

    public void Process(
      Scene input,
      string outputFilename,
      string outputDir,
      string inputFilename,
      bool checkOpenBoundaries,
      IMyBuildLogger logger)
    {
      this.CalculateAbsSpace(input.RootNode, MyModelProcessor.ToVRage(input.RootNode.Transform));
      if (this.m_meshes.Count != input.MeshCount)
        logger.LogMessage(MessageType.Error, "Sections may not properly work if \"Preserve Instances\" is not unchecked when exporting FBX model");
      this.CalculateMetadata(input.RootNode, MyModelProcessor.ToVRage(input.RootNode.Transform), true, logger);
      this.CheckTextureChannels(input);
      this.FillLists(input, logger);
      this.RescaleAndRecenter(input);
      this.GenerateMeshParts(input);
      this.GenerateMeshSections(input);
      for (int index = 0; index < this.m_meshes.Count; ++index)
        this.ConvertMaterial(input.Materials[input.Meshes[this.m_meshes[index].MeshIndex].MaterialIndex], outputDir, logger, outputFilename);
      this.ProcessSkeleton(input);
      this.FillBonedata(input);
      this.ProcessAnimations(input);
      float? boneGridMapping = this.BoneGridMapping;
      if (boneGridMapping.HasValue)
      {
        List<Vector3I> boneMapping = (List<Vector3I>) null;
        if (this.BoneMapping != null)
          boneMapping = ((IEnumerable<Vector3>) this.BoneMapping).Select<Vector3, Vector3I>((Func<Vector3, Vector3I>) (s => Vector3I.Round(new Vector3(s.X, s.Y, s.Z)))).ToList<Vector3I>();
        boneGridMapping = this.BoneGridMapping;
        this.CreateBones(boneGridMapping.Value, boneMapping);
        this.BoneMapping = this.m_boneMapping.Select<Vector3I, Vector3>((Func<Vector3I, Vector3>) (s => new Vector3((float) s.X, (float) s.Y, (float) s.Z))).ToArray<Vector3>();
      }
      this.RescaleAndRecenterAnimations();
      if (checkOpenBoundaries)
      {
        List<Vector3> openBoundaries = new List<Vector3>();
        int[] array = this.m_MeshPartSolver.GetMeshPartContainer().SelectMany<KeyValuePair<int, MyMeshPartInfo>, int>((Func<KeyValuePair<int, MyMeshPartInfo>, IEnumerable<int>>) (x => (IEnumerable<int>) x.Value.m_indices)).ToArray<int>();
        MyUtils.GetOpenBoundaries(this.m_vertices.ToArray(), array, openBoundaries);
        if (openBoundaries.Count > 0)
          logger.LogMessage(MessageType.Error, "Model contains open boundaries (" + (object) openBoundaries.Count + " edges)");
      }
      Dictionary<string, object> tags = this.ExportModelDataToTags(outputFilename, outputDir, inputFilename, logger);
      string outputPath = MyModelProcessor.GetOutputPath(outputFilename, outputDir);
      Trace.WriteLine(outputPath);
      try
      {
        this.EnsureDirectory(outputPath, logger);
        MyModelExporter.ExportModelData(outputPath, tags);
      }
      catch (DirectoryNotFoundException ex)
      {
        logger.LogMessage(MessageType.Error, "Model couldn't been export to path: " + outputPath + " - this path was not found.");
      }
    }

    public void ExportSharedStub(
      string filename,
      string outputDir,
      string lod0GeomPath,
      IMyBuildLogger logger)
    {
      string outputPath = MyModelProcessor.GetOutputPath(filename, outputDir);
      bool flag = this.m_blendWeights.Count > 0 || this.m_blendIndices.Count > 0;
      Dictionary<string, object> tags = this.ExportModelDataToTags(filename, outputDir, filename, logger);
      tags["GeometryDataAsset"] = (object) lod0GeomPath;
      tags["IsSkinned"] = (object) flag;
      Trace.WriteLine(outputPath);
      try
      {
        this.EnsureDirectory(outputPath, logger);
        MyModelExporter.ExportModelData(outputPath, tags, false);
      }
      catch (DirectoryNotFoundException ex)
      {
        logger.LogMessage(MessageType.Error, "Model couldn't been export to path: " + outputPath + " - this path was not found.");
      }
    }

    private void EnsureDirectory(string exportFileName, IMyBuildLogger logger)
    {
      try
      {
        string directoryName = Path.GetDirectoryName(Path.GetFullPath(exportFileName));
        if (Directory.Exists(directoryName))
          return;
        Directory.CreateDirectory(directoryName);
        logger.LogMessage(MessageType.Info, "Created directory: " + directoryName);
      }
      catch (Exception ex)
      {
        logger.LogMessage(MessageType.Error, "Error checking the path: " + exportFileName + " Message:" + ex.Message);
      }
    }

    private void FillBonedata(Scene input)
    {
      for (int index1 = 0; index1 < input.MeshCount; ++index1)
      {
        if (input.Meshes[index1].HasBones)
        {
          for (int key = 0; key < input.Meshes[index1].VertexCount; ++key)
          {
            float[] numArray1 = new float[4];
            float[] array1 = this.vertToBoneWeight[index1][key].Select<VertexWeight, float>((Func<VertexWeight, float>) (w => w.Weight)).ToArray<float>();
            byte[] numArray2 = new byte[4];
            byte[] array2 = this.vertToBoneWeight[index1][key].Select<VertexWeight, byte>((Func<VertexWeight, byte>) (w => (byte) w.VertexID)).ToArray<byte>();
            for (int index2 = 0; index2 < ((IEnumerable<float>) array1).Count<float>() && index2 < 4; ++index2)
            {
              numArray1[index2] = array1[index2];
              numArray2[index2] = array2[index2];
            }
            this.m_blendWeights.Add(new Vector4(numArray1[0], numArray1[1], numArray1[2], numArray1[3]));
            this.m_blendIndices.Add(new Vector4I((int) numArray2[0], (int) numArray2[1], (int) numArray2[2], (int) numArray2[3]));
          }
        }
      }
    }

    private void ProcessAnimations(Scene input) => this.ProcessAnimationsRecursive(input);

    private void ProcessAnimationsRecursive(Scene input)
    {
      Matrix matrix1 = Matrix.Identity;
      if ((double) this.RotationX != 0.0 || (double) this.RotationY != 0.0 || (double) this.RotationZ != 0.0)
        matrix1 = Matrix.CreateRotationX(MathHelper.ToRadians(this.RotationX)) * Matrix.CreateRotationY(MathHelper.ToRadians(this.RotationY)) * Matrix.CreateRotationZ(MathHelper.ToRadians(this.RotationZ));
      Matrix matrix2 = Matrix.Invert(matrix1);
      foreach (Animation animation in input.Animations)
      {
        string name = animation.Name;
        MyAnimationClip myAnimationClip;
        if (!this.clips.TryGetValue(name, out myAnimationClip))
        {
          myAnimationClip = new MyAnimationClip();
          this.m_modelAnimations.Clips.Add(myAnimationClip);
          this.clips[name] = myAnimationClip;
          myAnimationClip.Name = name;
          for (int index = 0; index < this.m_bones.Count; ++index)
            myAnimationClip.Bones.Add(new MyAnimationClip.Bone()
            {
              Name = this.m_bones[index].Name
            });
        }
        if (animation.DurationInTicks / animation.TicksPerSecond > myAnimationClip.Duration)
          myAnimationClip.Duration = animation.DurationInTicks / animation.TicksPerSecond;
        SortedDictionary<double, MyAnimationClip.Keyframe> keyTransforms = new SortedDictionary<double, MyAnimationClip.Keyframe>();
        foreach (NodeAnimationChannel animationChannel in animation.NodeAnimationChannels)
        {
          string nodeName = animationChannel.NodeName;
          bool flag1 = true;
          bool flag2 = true;
          bool flag3 = true;
          bool flag4 = true;
          int index;
          if (this.m_bonesToIndex.TryGetValue(nodeName, out index))
          {
            if (flag1)
              keyTransforms.Clear();
            Vector3 zero = Vector3.Zero;
            if (flag2)
            {
              foreach (QuaternionKey rotationKey in animationChannel.RotationKeys)
              {
                Matrix vrage = MyModelProcessor.ToVRage((Matrix4x4) rotationKey.Value.GetMatrix());
                MyDebug.Assert(vrage.IsValid(), file: "E:\\Repo3\\Sources\\Tools\\MwmBuilder\\MyModelProcessor.cs", line: 495);
                VRageMath.Quaternion fromRotationMatrix = VRageMath.Quaternion.CreateFromRotationMatrix(vrage);
                if (!keyTransforms.TryGetValue(rotationKey.Time, out MyAnimationClip.Keyframe _))
                {
                  SortedDictionary<double, MyAnimationClip.Keyframe> sortedDictionary = keyTransforms;
                  double time = rotationKey.Time;
                  MyAnimationClip.Keyframe keyframe = new MyAnimationClip.Keyframe();
                  keyframe.Time = rotationKey.Time / animation.TicksPerSecond;
                  keyframe.Rotation = fromRotationMatrix;
                  keyframe.Translation = zero;
                  sortedDictionary.Add(time, keyframe);
                }
                else
                  keyTransforms[rotationKey.Time].Rotation = fromRotationMatrix;
              }
            }
            if (flag3)
            {
              double lastTimeKey = -1.0;
              foreach (VectorKey positionKey in animationChannel.PositionKeys)
              {
                Vector3 vrage = MyModelProcessor.ToVRage(positionKey.Value);
                MyAnimationClip.Keyframe transformKey;
                if (!keyTransforms.TryGetValue(positionKey.Time, out transformKey))
                {
                  double time = positionKey.Time;
                  transformKey = new MyAnimationClip.Keyframe()
                  {
                    Time = time / animation.TicksPerSecond
                  };
                  MyModelProcessor.InterpolateClosestRotationKeyframes(ref transformKey, keyTransforms, time);
                }
                transformKey.Translation = vrage;
                keyTransforms[positionKey.Time] = transformKey;
                this.InterpolatePositionKeyframes(keyTransforms, lastTimeKey, positionKey.Time);
                lastTimeKey = positionKey.Time;
              }
              this.InterpolatePositionKeyframes(keyTransforms, lastTimeKey, -1.0);
            }
            if (flag4)
            {
              foreach (KeyValuePair<double, MyAnimationClip.Keyframe> keyValuePair in keyTransforms)
              {
                Matrix fromQuaternion = Matrix.CreateFromQuaternion(keyValuePair.Value.Rotation);
                fromQuaternion.Translation = keyValuePair.Value.Translation * this.RescaleFactor;
                Matrix matrix3 = matrix2 * fromQuaternion * matrix1;
                VRageMath.Quaternion fromRotationMatrix = VRageMath.Quaternion.CreateFromRotationMatrix(matrix3);
                keyValuePair.Value.Rotation = fromRotationMatrix;
                keyValuePair.Value.Translation = matrix3.Translation;
                myAnimationClip.Bones[index].Keyframes.Add(keyValuePair.Value);
              }
            }
          }
        }
        if (false)
        {
          foreach (MyAnimationClip.Bone bone1 in myAnimationClip.Bones)
          {
            if (bone1.Keyframes.Count == 0)
            {
              VRageRender.Import.Bone bone2 = (VRageRender.Import.Bone) null;
              foreach (VRageRender.Import.Bone bone3 in this.m_bones)
              {
                if (bone3.Name == bone1.Name)
                {
                  bone2 = bone3;
                  break;
                }
              }
              if (bone2 != null)
              {
                List<MyAnimationClip.Keyframe> keyframes = bone1.Keyframes;
                MyAnimationClip.Keyframe keyframe = new MyAnimationClip.Keyframe();
                keyframe.Time = 0.0;
                keyframe.Rotation = VRageMath.Quaternion.CreateFromRotationMatrix(bone2.LocalTransform);
                keyframe.Translation = bone2.LocalTransform.Translation;
                keyframes.Add(keyframe);
              }
            }
          }
        }
      }
    }

    private void InterpolatePositionKeyframes(
      SortedDictionary<double, MyAnimationClip.Keyframe> keyTransforms,
      double lastTimeKey,
      double timeKey)
    {
      if (lastTimeKey < 0.0 && timeKey < 0.0)
        return;
      if (lastTimeKey < 0.0)
      {
        Vector3 translation = keyTransforms[timeKey].Translation;
        foreach (KeyValuePair<double, MyAnimationClip.Keyframe> keyTransform in keyTransforms)
        {
          if (keyTransform.Key < timeKey)
            keyTransform.Value.Translation = translation;
        }
      }
      else if (timeKey < 0.0)
      {
        Vector3 translation = keyTransforms[lastTimeKey].Translation;
        foreach (KeyValuePair<double, MyAnimationClip.Keyframe> keyTransform in keyTransforms)
        {
          if (keyTransform.Key > lastTimeKey)
            keyTransform.Value.Translation = translation;
        }
      }
      else
      {
        Vector3 translation1 = keyTransforms[lastTimeKey].Translation;
        Vector3 translation2 = keyTransforms[timeKey].Translation;
        foreach (KeyValuePair<double, MyAnimationClip.Keyframe> keyTransform in keyTransforms)
        {
          if (keyTransform.Key >= lastTimeKey && keyTransform.Key <= timeKey)
            keyTransform.Value.Translation = Vector3.Lerp(translation1, translation2, (float) ((keyTransform.Key - lastTimeKey) / (timeKey - lastTimeKey)));
        }
      }
    }

    private static void InterpolateClosestRotationKeyframes(
      ref MyAnimationClip.Keyframe transformKey,
      SortedDictionary<double, MyAnimationClip.Keyframe> keyTransforms,
      double timeKey)
    {
      VRageMath.Quaternion quaternion1 = VRageMath.Quaternion.Identity;
      double num1 = 0.0;
      VRageMath.Quaternion quaternion2 = VRageMath.Quaternion.Identity;
      double num2 = 0.0;
      bool flag = false;
      foreach (KeyValuePair<double, MyAnimationClip.Keyframe> keyTransform in keyTransforms)
      {
        if (keyTransform.Key <= timeKey || !flag)
        {
          quaternion2 = quaternion1 = keyTransform.Value.Rotation;
          num2 = num1 = keyTransform.Key;
          flag = true;
          if (keyTransform.Key > timeKey)
            break;
        }
        else
        {
          quaternion2 = keyTransform.Value.Rotation;
          num2 = keyTransform.Key;
          break;
        }
      }
      if (num2 - num1 <= 0.0)
        transformKey.Rotation = quaternion1;
      else
        transformKey.Rotation = VRageMath.Quaternion.Slerp(quaternion1, quaternion2, (float) ((timeKey - num1) / (num2 - num1)));
    }

    private void RescaleAndRecenterAnimations()
    {
      Matrix matrix1 = Matrix.Identity;
      if ((double) this.RotationX != 0.0 || (double) this.RotationY != 0.0 || (double) this.RotationZ != 0.0)
        matrix1 = Matrix.CreateRotationX(MathHelper.ToRadians(this.RotationX)) * Matrix.CreateRotationY(MathHelper.ToRadians(this.RotationY)) * Matrix.CreateRotationZ(MathHelper.ToRadians(this.RotationZ));
      Matrix matrix2 = Matrix.Invert(matrix1);
      for (int index = 0; index < this.m_nodes.Count; ++index)
      {
        VRageRender.Import.Bone bone = this.m_bonesByName[this.m_nodes[index].Name];
        bone.LocalTransform = matrix2 * bone.LocalTransform * matrix1;
      }
    }

    private void RescaleAndRecenter(Scene input)
    {
      for (int index = 0; index < this.m_vertices.Count; ++index)
        this.m_vertices[index] *= this.RescaleFactor;
      Matrix matrix1 = Matrix.Identity;
      if ((double) this.RotationX != 0.0 || (double) this.RotationY != 0.0 || (double) this.RotationZ != 0.0)
      {
        matrix1 = Matrix.CreateRotationX(MathHelper.ToRadians(this.RotationX)) * Matrix.CreateRotationY(MathHelper.ToRadians(this.RotationY)) * Matrix.CreateRotationZ(MathHelper.ToRadians(this.RotationZ));
        for (int index = 0; index < this.m_vertices.Count; ++index)
          this.m_vertices[index] = Vector3.Transform(this.m_vertices[index], matrix1);
        for (int index = 0; index < this.m_normals.Count; ++index)
          this.m_normals[index] = Vector3.TransformNormal(this.m_normals[index], matrix1);
        for (int index = 0; index < this.m_tangents.Count; ++index)
          this.m_tangents[index] = Vector3.TransformNormal(this.m_tangents[index], matrix1);
        for (int index = 0; index < this.m_binormals.Count; ++index)
          this.m_binormals[index] = Vector3.TransformNormal(this.m_binormals[index], matrix1);
      }
      Matrix matrix2 = Matrix.CreateScale(this.RescaleFactor) * matrix1;
      foreach (KeyValuePair<string, MyModelDummy> keyValuePair in new Dictionary<string, MyModelDummy>((IDictionary<string, MyModelDummy>) this.m_dummies))
      {
        Matrix matrix3 = matrix2 * this.m_dummies[keyValuePair.Key].Matrix;
        matrix3.Translation *= this.RescaleFactor;
        this.m_dummies[keyValuePair.Key].Matrix = matrix3;
      }
      this.CalcBoundingBox();
      this.CalcBoundingSphere();
    }

    private void CalculateMetadata(
      Assimp.Node root,
      Matrix parentMatrix,
      bool isRoot,
      IMyBuildLogger logger)
    {
      if (!root.HasChildren)
        return;
      foreach (Assimp.Node child in root.Children)
      {
        Matrix vrage = MyModelProcessor.ToVRage(child.Transform);
        Matrix matrix = MyModelProcessor.Identity3DSMax * vrage * MyModelProcessor.ToVRage(child.GeometricTranslation) * MyModelProcessor.ToVRage(child.GeometricRotation) * MyModelProcessor.ToVRage(child.GeometricScaling) * parentMatrix;
        Matrix parentMatrix1 = vrage * parentMatrix;
        if (child.Metadata.Count != 0 && !child.HasMeshes)
        {
          string key1 = child.Name;
          string oldValue = "dummy_";
          if (key1.ToLower().Contains(oldValue))
            key1 = key1.Replace(oldValue, "").Replace(oldValue.ToUpper(), "");
          MyModelDummy myModelDummy = new MyModelDummy()
          {
            Name = key1,
            Matrix = matrix,
            CustomData = new Dictionary<string, object>()
          };
          foreach (KeyValuePair<string, Metadata.Entry> keyValuePair in (Dictionary<string, Metadata.Entry>) child.Metadata)
          {
            string key2 = keyValuePair.Key.Trim('\n', ' ');
            myModelDummy.CustomData.Add(key2, keyValuePair.Value.Data);
          }
          try
          {
            this.m_dummies.Add(key1, myModelDummy);
          }
          catch (ArgumentException ex)
          {
            logger.LogMessage(MessageType.Error, "Model node " + key1 + " already exists. Please use unique names!");
          }
        }
        this.CalculateMetadata(child, parentMatrix1, false, logger);
      }
    }

    private void FillLists(Scene input, IMyBuildLogger logger)
    {
      this.SwapWindingOrder = false;
      int num1 = 0;
      for (int index1 = 0; index1 < this.m_meshes.Count; ++index1)
      {
        Assimp.Mesh mesh = input.Meshes[this.m_meshes[index1].MeshIndex];
        this.m_meshes[index1].VertexOffset = num1;
        this.m_meshes[index1].VertexCount = mesh.VertexCount;
        for (int index2 = 0; index2 < mesh.VertexCount; ++index2)
          this.m_vertices.Add(Vector3.Transform(MyModelProcessor.ToVRage(mesh.Vertices[index2]), this.m_meshes[index1].AbsoluteMatrix));
        num1 += mesh.VertexCount;
        if (!this.SimpleModel)
        {
          MyDebug.Assert(mesh.VertexCount == mesh.Normals.Count<Assimp.Vector3D>(), file: "E:\\Repo3\\Sources\\Tools\\MwmBuilder\\MyModelProcessor.cs", line: 832);
          for (int index2 = 0; index2 < mesh.VertexCount; ++index2)
            this.m_normals.Add(Vector3.Normalize(Vector3.TransformNormal(MyModelProcessor.ToVRage(mesh.Normals[index2]), this.m_meshes[index1].AbsoluteMatrix)));
          int num2 = mesh.Tangents.Count<Assimp.Vector3D>();
          if (mesh.VertexCount != num2)
            logger.LogMessage(MessageType.Error, "Mesh " + (string.IsNullOrEmpty(mesh.Name) ? "with index " + index1.ToString() : mesh.Name) + " has missing tangents, filling with default");
          for (int index2 = 0; index2 < mesh.VertexCount; ++index2)
            this.m_tangents.Add(Vector3.Normalize(Vector3.TransformNormal(index2 >= num2 ? Vector3.Forward : MyModelProcessor.ToVRage(mesh.Tangents[index2]), this.m_meshes[index1].AbsoluteMatrix)));
          int num3 = mesh.BiTangents.Count<Assimp.Vector3D>();
          if (mesh.VertexCount != num3)
            logger.LogMessage(MessageType.Error, "Mesh " + (string.IsNullOrEmpty(mesh.Name) ? "with index " + index1.ToString() : mesh.Name) + " has missing binormals, filling with default");
          for (int index2 = 0; index2 < mesh.VertexCount; ++index2)
            this.m_binormals.Add(Vector3.Normalize(Vector3.TransformNormal(index2 >= num3 ? Vector3.Forward : MyModelProcessor.ToVRage(mesh.BiTangents[index2]), this.m_meshes[index1].AbsoluteMatrix)));
          if (this.m_containsTexChannel0)
          {
            for (int index2 = 0; index2 < mesh.VertexCount; ++index2)
              this.m_texCoords0.Add(MyModelProcessor.ToVRageVector2FromVector3(mesh.TextureCoordinateChannels[0][index2]));
          }
          if (this.m_containsTexChannel1)
          {
            for (int index2 = 0; index2 < mesh.VertexCount; ++index2)
              this.m_texCoords1.Add(MyModelProcessor.ToVRageVector2FromVector3(mesh.TextureCoordinateChannels[1][index2]));
          }
        }
      }
    }

    public void CalculateAbsSpace(Assimp.Node root, Matrix transform)
    {
      if (!root.HasChildren)
        return;
      foreach (Assimp.Node child in root.Children)
      {
        Matrix transform1 = MyModelProcessor.ToVRage(child.Transform) * transform;
        Matrix matrix = MyModelProcessor.ToVRage(child.GeometricScaling) * MyModelProcessor.ToVRage(child.GeometricRotation) * MyModelProcessor.ToVRage(child.GeometricTranslation) * transform1;
        for (int index = 0; index < child.MeshCount; ++index)
        {
          int meshIndex = child.MeshIndices[index];
          if (!this.m_sourceMeshMap.ContainsKey(meshIndex))
            this.m_sourceMeshMap[meshIndex] = this.m_meshes.Count;
          this.m_meshes.Add(new VRageRender.Import.Mesh()
          {
            AbsoluteMatrix = matrix,
            MeshIndex = meshIndex
          });
        }
        this.CalculateAbsSpace(child, transform1);
      }
    }

    private void CalcBoundingBox()
    {
      this.m_boundingBox.Min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
      this.m_boundingBox.Max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
      for (int index = 0; index < this.m_vertices.Count; ++index)
      {
        Vector3 vertex = this.m_vertices[index];
        Vector3.Min(ref this.m_boundingBox.Min, ref vertex, out this.m_boundingBox.Min);
        Vector3.Max(ref this.m_boundingBox.Max, ref vertex, out this.m_boundingBox.Max);
      }
    }

    private void CalcBoundingSphere()
    {
      this.m_boundingSphere.Center = (this.m_boundingBox.Max + this.m_boundingBox.Min) / 2f;
      this.m_boundingSphere.Radius = 0.0f;
      for (int index = 0; index < this.m_vertices.Count; ++index)
      {
        float num = Vector3.Distance(this.m_boundingSphere.Center, this.m_vertices[index]);
        if ((double) num > (double) this.m_boundingSphere.Radius)
          this.m_boundingSphere.Radius = num;
      }
    }

    private VRageRender.Import.Bone ProcessSkeleton(Scene input)
    {
      VRageRender.Import.Bone boneTree = this.CreateBoneTree(input.RootNode, (VRageRender.Import.Bone) null, 0);
      if (boneTree == null)
        return (VRageRender.Import.Bone) null;
      this.m_bones.Clear();
      foreach (KeyValuePair<string, VRageRender.Import.Bone> keyValuePair in this.m_bonesByName)
      {
        this.m_bones.Add(keyValuePair.Value);
        this.m_bonesToIndex[keyValuePair.Key] = this.m_bones.IndexOf(keyValuePair.Value);
      }
      foreach (Assimp.Node node in this.FlattenHeirarchy(input.RootNode))
        this.m_nodes.Add(new NodeDesc()
        {
          Name = node.Name,
          ParentName = node.Parent != null ? node.Parent.Name : (string) null
        });
      foreach (NodeDesc node1 in this.m_nodes)
      {
        NodeDesc node = node1;
        if (!string.IsNullOrEmpty(node.ParentName))
          node.Parent = this.m_nodes.Find((Predicate<NodeDesc>) (x => x.Name == node.ParentName));
      }
      for (int index = 0; index < this.m_nodes.Count; ++index)
        this.m_nodeToIndex[this.m_nodes[index].Name] = index;
      for (int index = 0; index < this.m_meshes.Count; ++index)
      {
        Assimp.Mesh mesh = input.Meshes[this.m_meshes[index].MeshIndex];
        this.vertToBoneWeight.Add(new Dictionary<int, List<VertexWeight>>());
        this.ExtractBoneWeightsFromMesh(mesh, (IDictionary<int, List<VertexWeight>>) this.vertToBoneWeight[index]);
      }
      this.FlattenTransforms(input.RootNode, boneTree, input);
      foreach (NodeDesc node in this.m_nodes)
      {
        VRageRender.Import.Bone bone = this.m_bonesByName[node.Name];
        Matrix localTransform = bone.LocalTransform;
        localTransform.Translation *= this.RescaleFactor;
        bone.LocalTransform = localTransform;
      }
      foreach (VRageRender.Import.Bone bone in this.m_bones)
        this.m_modelAnimations.Skeleton.Add(this.m_nodeToIndex[bone.Name]);
      return boneTree;
    }

    private List<Assimp.Node> FlattenHeirarchy(Assimp.Node item)
    {
      List<Assimp.Node> nodes = new List<Assimp.Node>();
      nodes.Add(item);
      foreach (Assimp.Node child in item.Children)
        this.FlattenHeirarchy(nodes, child);
      return nodes;
    }

    private void FlattenHeirarchy(List<Assimp.Node> nodes, Assimp.Node item)
    {
      nodes.Add(item);
      foreach (Assimp.Node child in item.Children)
        this.FlattenHeirarchy(nodes, child);
    }

    private void ExtractBoneWeightsFromMesh(
      Assimp.Mesh mesh,
      IDictionary<int, List<VertexWeight>> vertToBoneWeight)
    {
      foreach (Assimp.Bone bone in mesh.Bones)
      {
        int vertID = this.m_bonesToIndex[bone.Name];
        foreach (VertexWeight vertexWeight in bone.VertexWeights)
        {
          if (vertToBoneWeight.ContainsKey(vertexWeight.VertexID))
            vertToBoneWeight[vertexWeight.VertexID].Add(new VertexWeight(vertID, vertexWeight.Weight));
          else
            vertToBoneWeight[vertexWeight.VertexID] = new List<VertexWeight>((IEnumerable<VertexWeight>) new VertexWeight[1]
            {
              new VertexWeight(vertID, vertexWeight.Weight)
            });
        }
      }
    }

    private void FlattenTransforms(Assimp.Node node, VRageRender.Import.Bone skeleton, Scene input)
    {
      foreach (Assimp.Node child in node.Children)
      {
        if (!(child.Name == skeleton.Name))
        {
          if (this.IsSkinned(child, input))
            this.FlattenAllTransforms(child, input);
          else
            this.FlattenTransforms(child, skeleton, input);
        }
      }
    }

    private void FlattenAllTransforms(Assimp.Node node, Scene input)
    {
      this.TransformScene(node, node.Transform, input);
      node.Transform = Matrix4x4.Identity;
      foreach (Assimp.Node child in node.Children)
        this.FlattenAllTransforms(child, input);
    }

    private void TransformScene(Assimp.Node node, Matrix4x4 transform, Scene input)
    {
      if (!node.HasChildren)
        return;
      for (int index1 = 0; index1 < node.MeshCount; ++index1)
      {
        List<Vector3> vector3List1 = new List<Vector3>();
        List<Vector3> vector3List2 = new List<Vector3>();
        List<Vector3> vector3List3 = new List<Vector3>();
        List<Vector3> vector3List4 = new List<Vector3>();
        for (int index2 = 0; index2 < input.Meshes[node.MeshIndices[index1]].VertexCount; ++index2)
        {
          vector3List1.Add(Vector3.Transform(MyModelProcessor.ToVRage(input.Meshes[node.MeshIndices[index1]].Vertices[index2]), MyModelProcessor.ToVRage(transform)));
          input.Meshes[node.MeshIndices[index1]].Vertices[index2] = MyModelProcessor.ToAssimp(vector3List1[index2]);
        }
        MyDebug.Assert(input.Meshes[node.MeshIndices[index1]].VertexCount == input.Meshes[node.MeshIndices[index1]].Normals.Count<Assimp.Vector3D>(), file: "E:\\Repo3\\Sources\\Tools\\MwmBuilder\\MyModelProcessor.cs", line: 1152);
        for (int index2 = 0; index2 < input.Meshes[node.MeshIndices[index1]].VertexCount; ++index2)
        {
          vector3List2.Add(Vector3.Normalize(Vector3.TransformNormal(MyModelProcessor.ToVRage(input.Meshes[node.MeshIndices[index1]].Normals[index2]), MyModelProcessor.ToVRage(transform))));
          input.Meshes[node.MeshIndices[index1]].Normals[index2] = MyModelProcessor.ToAssimp(vector3List2[index2]);
        }
        MyDebug.Assert(input.Meshes[node.MeshIndices[index1]].VertexCount == input.Meshes[node.MeshIndices[index1]].Tangents.Count<Assimp.Vector3D>(), file: "E:\\Repo3\\Sources\\Tools\\MwmBuilder\\MyModelProcessor.cs", line: 1159);
        for (int index2 = 0; index2 < input.Meshes[node.MeshIndices[index1]].VertexCount; ++index2)
        {
          vector3List4.Add(Vector3.Normalize(Vector3.TransformNormal(MyModelProcessor.ToVRage(input.Meshes[node.MeshIndices[index1]].Tangents[index2]), MyModelProcessor.ToVRage(transform))));
          input.Meshes[node.MeshIndices[index1]].Tangents[index2] = MyModelProcessor.ToAssimp(vector3List4[index2]);
        }
        MyDebug.Assert(input.Meshes[node.MeshIndices[index1]].VertexCount == input.Meshes[node.MeshIndices[index1]].BiTangents.Count<Assimp.Vector3D>(), file: "E:\\Repo3\\Sources\\Tools\\MwmBuilder\\MyModelProcessor.cs", line: 1166);
        for (int index2 = 0; index2 < input.Meshes[node.MeshIndices[index1]].VertexCount; ++index2)
        {
          vector3List3.Add(Vector3.Normalize(Vector3.TransformNormal(MyModelProcessor.ToVRage(input.Meshes[node.MeshIndices[index1]].BiTangents[index2]), MyModelProcessor.ToVRage(transform))));
          input.Meshes[node.MeshIndices[index1]].BiTangents[index2] = MyModelProcessor.ToAssimp(vector3List3[index2]);
        }
      }
      foreach (Assimp.Node child in node.Children)
        this.TransformScene(child, node.Transform, input);
    }

    private bool IsSkinned(Assimp.Node node, Scene input)
    {
      for (int index1 = 0; index1 < node.MeshCount; ++index1)
      {
        for (int index2 = 0; index2 < input.Meshes[node.MeshIndices[index1]].VertexCount; ++index2)
        {
          if (this.vertToBoneWeight[index2] != null || this.vertToBoneWeight[index2].Count > 0)
            return true;
        }
      }
      return false;
    }

    private VRageRender.Import.Bone CreateBoneTree(Assimp.Node node, VRageRender.Import.Bone parent, int temp)
    {
      VRageRender.Import.Bone parent1 = new VRageRender.Import.Bone()
      {
        Name = node.Name,
        Parent = parent
      };
      if (parent1.Name == "")
        parent1.Name = "foo" + (object) temp++;
      this.m_bonesByName[parent1.Name] = parent1;
      parent1.LocalTransform = MyModelProcessor.ToVRage(node.Transform);
      for (int index = 0; index < node.ChildCount; ++index)
      {
        VRageRender.Import.Bone boneTree = this.CreateBoneTree(node.Children[index], parent1, temp);
        if (boneTree != null)
          parent1.Children.Add(boneTree);
      }
      return parent1;
    }

    private Vector3 BoneOffsetToPosition(Vector3I offset, float gridSize) => new Vector3((float) offset.X, (float) offset.Y, (float) offset.Z) / 2f * gridSize - new Vector3(gridSize / 2f);

    private void CreateBones(float gridSize, List<Vector3I> boneMapping)
    {
      this.m_boneMapping = boneMapping ?? this.ChooseBones(9, gridSize);
      this.m_blendIndices = new List<Vector4I>(this.m_vertices.Count);
      this.m_blendWeights = new List<Vector4>(this.m_vertices.Count);
      for (int i = 0; i < this.m_vertices.Count; ++i)
        this.CalculateBone(gridSize, i);
      if (this.LODs.Length == 0)
        return;
      List<int> usedBones = new List<int>();
      for (int index = 0; index < this.m_vertices.Count; ++index)
      {
        if ((double) this.m_blendWeights[index].X != 0.0 && !usedBones.Contains(this.m_blendIndices[index].X))
          usedBones.Add(this.m_blendIndices[index].X);
        if ((double) this.m_blendWeights[index].Y != 0.0 && !usedBones.Contains(this.m_blendIndices[index].Y))
          usedBones.Add(this.m_blendIndices[index].Y);
        if ((double) this.m_blendWeights[index].Z != 0.0 && !usedBones.Contains(this.m_blendIndices[index].Z))
          usedBones.Add(this.m_blendIndices[index].Z);
        if ((double) this.m_blendWeights[index].W != 0.0 && !usedBones.Contains(this.m_blendIndices[index].W))
          usedBones.Add(this.m_blendIndices[index].W);
      }
      usedBones.Sort();
      int[] array = Enumerable.Range(0, this.m_boneMapping.Count).Select<int, int>((Func<int, int>) (s => usedBones.IndexOf(s))).ToArray<int>();
      this.m_boneMapping = usedBones.Select<int, Vector3I>((Func<int, Vector3I>) (val => this.m_boneMapping[val])).ToList<Vector3I>();
      for (int index = 0; index < this.m_vertices.Count; ++index)
      {
        Vector4I blendIndex = this.m_blendIndices[index];
        if ((double) this.m_blendWeights[index].X != 0.0)
          blendIndex.X = array[blendIndex.X];
        if ((double) this.m_blendWeights[index].Y != 0.0)
          blendIndex.Y = array[blendIndex.Y];
        if ((double) this.m_blendWeights[index].Z != 0.0)
          blendIndex.Z = array[blendIndex.Z];
        if ((double) this.m_blendWeights[index].W != 0.0)
          blendIndex.W = array[blendIndex.W];
        this.m_blendIndices[index] = blendIndex;
      }
    }

    private List<Vector3I> ChooseBones(int count, float gridSize)
    {
      this.m_boneOffsetHelper.Clear();
      for (int z = 0; z < 3; ++z)
      {
        for (int y = 0; y < 3; ++y)
        {
          for (int x = 0; x < 3; ++x)
          {
            Vector3I offset = new Vector3I(x, y, z);
            Vector3 position = this.BoneOffsetToPosition(offset, gridSize);
            foreach (Vector3 vertex in this.m_vertices)
              this.m_boneOffsetHelper.Add(new Tuple<float, Vector3I>(Vector3.DistanceSquared(vertex, position), offset));
          }
        }
      }
      return this.m_boneOffsetHelper.OrderBy<Tuple<float, Vector3I>, float>((Func<Tuple<float, Vector3I>, float>) (s => s.Item1)).Select<Tuple<float, Vector3I>, Vector3I>((Func<Tuple<float, Vector3I>, Vector3I>) (s => s.Item2)).Distinct<Vector3I>().Take<Vector3I>(count).ToList<Vector3I>();
    }

    private void CalculateBone(float gridSize, int i)
    {
      this.m_boneIndexHelper.Clear();
      Vector3 vertex = this.m_vertices[i];
      for (int index = 0; index < this.m_boneMapping.Count; ++index)
      {
        float num = Vector3.Distance(this.BoneOffsetToPosition(this.m_boneMapping[index], gridSize), vertex);
        if ((double) num < 9.99999974737875E-05)
        {
          this.m_blendIndices.Add(new Vector4I(index, 0, 0, 0));
          this.m_blendWeights.Add(new Vector4(1f, 0.0f, 0.0f, 0.0f));
          return;
        }
        this.m_boneIndexHelper.Add(new Tuple<float, int>(MathHelper.Clamp((float) (1.0 - (double) num / ((double) gridSize / 2.0)), 0.0f, 1f), index));
      }
      this.m_boneIndexHelper.Sort(new Comparison<Tuple<float, int>>(this.Comparer));
      Vector4 vector4 = new Vector4(this.GetWeight(this.m_boneIndexHelper, 0), this.GetWeight(this.m_boneIndexHelper, 1), this.GetWeight(this.m_boneIndexHelper, 2), this.GetWeight(this.m_boneIndexHelper, 3));
      Vector4I vector4I = new Vector4I(this.GetIndex(this.m_boneIndexHelper, 0), this.GetIndex(this.m_boneIndexHelper, 1), this.GetIndex(this.m_boneIndexHelper, 2), this.GetIndex(this.m_boneIndexHelper, 3));
      float num1 = vector4.X + vector4.Y + vector4.Z + vector4.W;
      if ((double) num1 != 0.0)
        vector4 /= num1;
      this.DiscardLowWeight(ref vector4.X, ref vector4I.X);
      this.DiscardLowWeight(ref vector4.Y, ref vector4I.Y);
      this.DiscardLowWeight(ref vector4.Z, ref vector4I.Z);
      this.DiscardLowWeight(ref vector4.W, ref vector4I.W);
      float num2 = vector4.X + vector4.Y + vector4.Z + vector4.W;
      if ((double) num2 != 0.0)
        vector4 /= num2;
      this.m_blendIndices.Add(vector4I);
      this.m_blendWeights.Add(vector4);
    }

    private void DiscardLowWeight(ref float weight, ref int index)
    {
      if ((double) weight >= 0.0500000007450581)
        return;
      weight = 0.0f;
      index = 0;
    }

    private int GetIndex(List<Tuple<float, int>> bones, int index) => index >= bones.Count ? 0 : bones[index].Item2;

    private float GetWeight(List<Tuple<float, int>> bones, int index) => index >= bones.Count ? 0.0f : bones[index].Item1;

    private int Comparer(Tuple<float, int> a, Tuple<float, int> b) => -a.Item1.CompareTo(b.Item1);

    public static string GetOutputPath(string sourcePath, string outputDir)
    {
      if (outputDir == null)
        return Path.ChangeExtension(sourcePath, MyModelProcessor.C_MINER_WARS_MODEL_FORMAT_EXT);
      string sourceFile = sourcePath;
      string str1 = outputDir;
      string resourcePathInContent = MyModelProcessor.GetResourcePathInContent(sourceFile);
      string str2 = Path.GetDirectoryName(resourcePathInContent) + "\\" + (Path.GetFileNameWithoutExtension(resourcePathInContent) + MyModelProcessor.C_MINER_WARS_MODEL_FORMAT_EXT);
      string str3 = str1.TrimEnd('\\');
      if (Path.GetFileName(str3) != MyModelProcessor.ContentRootName)
      {
        int length = MyModelProcessor.GetResourcePathInContent(str3).Trim('\\').Split('\\').Length;
        string str4 = str2.Trim('\\');
        char[] chArray = new char[1]{ '\\' };
        List<string> list;
        for (list = ((IEnumerable<string>) str4.Split(chArray)).ToList<string>(); length > 0 && list.Count > 1; --length)
          list.RemoveAt(0);
        str2 = Path.Combine(list.ToArray());
      }
      return Path.Combine(str3, str2.Trim('\\'));
    }

    public static string GetResourcePathInContent(string sourceFile)
    {
      if (!sourceFile.Contains(MyModelProcessor.ContentRootName))
        return "\\.\\" + Path.GetFileName(sourceFile);
      int startIndex = sourceFile.LastIndexOf(MyModelProcessor.ContentRootName) + MyModelProcessor.ContentRootName.Length;
      return sourceFile.Substring(startIndex, sourceFile.Length - startIndex);
    }

    private MyModelBone[] CreateBonesForExport(
      Dictionary<string, int> nodeIndex,
      Dictionary<string, VRageRender.Import.Bone> boneByName,
      List<NodeDesc> nodes)
    {
      List<MyModelBone> myModelBoneList = new List<MyModelBone>();
      foreach (NodeDesc node in nodes)
        myModelBoneList.Add(new MyModelBone()
        {
          Name = node.Name,
          Parent = node.Parent == null ? -1 : nodeIndex[node.Parent.Name],
          Transform = boneByName[node.Name].LocalTransform
        });
      return myModelBoneList.ToArray();
    }

    private Dictionary<string, object> ExportModelDataToTags(
      string outputFilename,
      string outputDir,
      string srcFilename,
      IMyBuildLogger logger)
    {
      Dictionary<string, object> dictionary = new Dictionary<string, object>();
      dictionary["Debug"] = (object) this.m_debug.ToArray();
      dictionary["Dummies"] = (object) this.m_dummies;
      this.m_packedVertices.Clear();
      foreach (Vector3 vertex in this.m_vertices)
        this.m_packedVertices.Add(VF_Packer.PackPosition(vertex));
      dictionary["Vertices"] = (object) this.m_packedVertices.ToArray();
      dictionary["Normals"] = (object) this.m_normals.ConvertAll<Byte4>((Converter<Vector3, Byte4>) (x => new Byte4(VF_Packer.PackNormal(x)))).ToArray();
      dictionary["TexCoords0"] = this.m_containsTexChannel0 ? (object) this.m_texCoords0.ConvertAll<HalfVector2>((Converter<Vector2, HalfVector2>) (x => new HalfVector2(x))).ToArray() : (object) (HalfVector2[]) null;
      dictionary["Binormals"] = this.m_containsTexChannel0 ? (object) this.m_binormals.ConvertAll<Byte4>((Converter<Vector3, Byte4>) (x => new Byte4(VF_Packer.PackNormal(x)))).ToArray() : (object) (Byte4[]) null;
      dictionary["Tangents"] = this.m_containsTexChannel0 ? (object) this.m_tangents.ConvertAll<Byte4>((Converter<Vector3, Byte4>) (x => new Byte4(VF_Packer.PackNormal(x)))).ToArray() : (object) (Byte4[]) null;
      dictionary["TexCoords1"] = this.m_containsTexChannel1 ? (object) this.m_texCoords1.ConvertAll<HalfVector2>((Converter<Vector2, HalfVector2>) (x => new HalfVector2(x))).ToArray() : (object) (HalfVector2[]) null;
      dictionary["RescaleFactor"] = (object) this.RescaleFactor;
      dictionary["UseChannelTextures"] = (object) this.UseChannelTextures;
      dictionary["BoundingBox"] = (object) this.m_boundingBox;
      dictionary["BoundingSphere"] = (object) this.m_boundingSphere;
      dictionary["SwapWindingOrder"] = (object) this.SwapWindingOrder;
      dictionary["MeshParts"] = (object) this.m_MeshPartSolver.GetMeshPartContainer().Select<KeyValuePair<int, MyMeshPartInfo>, MyMeshPartInfo>((Func<KeyValuePair<int, MyMeshPartInfo>, MyMeshPartInfo>) (x => x.Value)).ToList<MyMeshPartInfo>();
      dictionary["Sections"] = (object) this.m_sections;
      this.ReportDegeneratedTriangles(logger, srcFilename);
      dictionary["ModelBvh"] = (object) this.CalculateBvh();
      dictionary["ModelInfo"] = (object) this.CreateModelInfo();
      dictionary["BlendIndices"] = (object) this.m_blendIndices.ToArray();
      dictionary["BlendWeights"] = (object) this.m_blendWeights.ToArray();
      dictionary["Animations"] = (object) this.m_modelAnimations;
      MyModelBone[] bonesForExport = this.CreateBonesForExport(this.m_nodeToIndex, this.m_bonesByName, this.m_nodes);
      dictionary["Bones"] = (object) bonesForExport;
      dictionary["BoneMapping"] = (object) this.m_boneMapping.ToArray();
      dictionary["HavokCollisionGeometry"] = (object) this.HavokCollisionShapes;
      dictionary["PatternScale"] = (object) this.PatternScale;
      dictionary["LODs"] = (object) this.LODs;
      dictionary["FBXHash"] = (object) ModelAutoRebuild.GetFileHash(srcFilename);
      if (File.Exists(Path.ChangeExtension(srcFilename, ".hkt")))
        dictionary["HKTHash"] = (object) ModelAutoRebuild.GetFileHash(Path.ChangeExtension(srcFilename, ".hkt"));
      if (File.Exists(Path.ChangeExtension(srcFilename, ".xml")))
        dictionary["HKTHash"] = (object) ModelAutoRebuild.GetFileHash(Path.ChangeExtension(srcFilename, ".xml"));
      return dictionary;
    }

    private MyModelInfo CreateModelInfo()
    {
      int triCnt = 0;
      foreach (KeyValuePair<int, MyMeshPartInfo> keyValuePair in this.m_MeshPartSolver.GetMeshPartContainer())
        triCnt += keyValuePair.Value.m_indices.Count / 3;
      int count = this.m_vertices.Count;
      Vector3 BBsize = this.m_boundingBox.Max - this.m_boundingBox.Min;
      return new MyModelInfo(triCnt, count, BBsize);
    }

    void IPrimitiveManagerBase.Cleanup()
    {
    }

    bool IPrimitiveManagerBase.IsTrimesh() => true;

    int IPrimitiveManagerBase.GetPrimitiveCount() => this.m_bvhIndices.Count / 3;

    void IPrimitiveManagerBase.GetPrimitiveBox(
      int prim_index,
      out AABB primbox)
    {
      try
      {
        int index = prim_index * 3;
        Vector3 vector3_1 = new Vector3(float.PositiveInfinity);
        Vector3 vector3_2 = new Vector3(float.NegativeInfinity);
        Vector3 vector3_3 = Vector3.Min(vector3_1, this.m_vertices[this.m_bvhIndices[index]]);
        Vector3 vector3_4 = Vector3.Max(vector3_2, this.m_vertices[this.m_bvhIndices[index]]);
        Vector3 vector3_5 = Vector3.Min(vector3_3, this.m_vertices[this.m_bvhIndices[index + 1]]);
        Vector3 vector3_6 = Vector3.Max(vector3_4, this.m_vertices[this.m_bvhIndices[index + 1]]);
        Vector3 vector3_7 = Vector3.Min(vector3_5, this.m_vertices[this.m_bvhIndices[index + 2]]);
        Vector3 vector3_8 = Vector3.Max(vector3_6, this.m_vertices[this.m_bvhIndices[index + 2]]);
        primbox = new AABB()
        {
          m_min = new IndexedVector3(vector3_7.X, vector3_7.Y, vector3_7.Z),
          m_max = new IndexedVector3(vector3_8.X, vector3_8.Y, vector3_8.Z)
        };
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException("m_indices.count: " + (object) this.m_bvhIndices.Count + " PrimIndex: " + (object) prim_index + ", m_vert.count: " + (object) this.m_vertices.Count + ", firstTriIndex: " + (object) this.m_bvhIndices[prim_index * 3], ex);
      }
    }

    void IPrimitiveManagerBase.GetPrimitiveTriangle(
      int prim_index,
      PrimitiveTriangle triangle)
    {
      int bvhIndex = this.m_bvhIndices[prim_index * 3];
      Vector3 vertex1 = this.m_vertices[this.m_bvhIndices[bvhIndex]];
      Vector3 vertex2 = this.m_vertices[this.m_bvhIndices[bvhIndex + 1]];
      Vector3 vertex3 = this.m_vertices[this.m_bvhIndices[bvhIndex + 2]];
      triangle.m_vertices[0] = new IndexedVector3(vertex1.X, vertex1.Y, vertex1.Z);
      triangle.m_vertices[1] = new IndexedVector3(vertex2.X, vertex2.Y, vertex2.Z);
      triangle.m_vertices[2] = new IndexedVector3(vertex3.X, vertex3.Y, vertex3.Z);
    }

    public static Vector3 ToVRage(Assimp.Vector3D v) => new Vector3(v.X, v.Y, v.Z);

    private static VRageMath.Quaternion ToVRage(Assimp.Quaternion v) => new VRageMath.Quaternion(v.X, v.Y, v.Z, v.W);

    private static Assimp.Vector3D ToAssimp(Vector3 v) => new Assimp.Vector3D(v.X, v.Y, v.Z);

    public static Matrix ToVRage(Matrix4x4 v) => new Matrix(v.A1, v.B1, v.C1, v.D1, v.A2, v.B2, v.C2, v.D2, v.A3, v.B3, v.C3, v.D3, v.A4, v.B4, v.C4, v.D4);

    private static Matrix4x4 ToAssimp(Matrix v) => new Matrix4x4(v.M11, v.M21, v.M31, v.M41, v.M12, v.M22, v.M32, v.M42, v.M13, v.M23, v.M33, v.M43, v.M14, v.M24, v.M34, v.M44);

    private static Vector2 ToVRageVector2FromVector3(Assimp.Vector3D v) => new Vector2(v.X, 1f - v.Y);

    private void GenerateMeshParts(Scene input)
    {
      this.m_MeshPartSolver.Clear();
      for (int index = 0; index < this.m_meshes.Count; ++index)
      {
        VRageRender.Import.Mesh mesh1 = this.m_meshes[index];
        Assimp.Mesh mesh2 = input.Meshes[this.m_meshes[index].MeshIndex];
        int matHash = 0;
        if (input.Materials[mesh2.MaterialIndex] != null && input.Materials[mesh2.MaterialIndex].Name != null)
          matHash = input.Materials[mesh2.MaterialIndex].Name.GetHashCode();
        this.m_MeshPartSolver.SetIndices(mesh2, mesh1, mesh2.GetIndices(), this.m_vertices, matHash);
      }
    }

    private void GenerateMeshSections(Scene input)
    {
      foreach (Assimp.Node child in input.RootNode.Children[0].Children)
      {
        if (Regex.Match(child.Name, "^(?<prefix>.+)_section_?(?<suffix>.*)$").Success)
        {
          string name = child.Name;
          MyMeshSectionInfo myMeshSectionInfo = new MyMeshSectionInfo()
          {
            Name = name
          };
          foreach (int meshIndex in child.MeshIndices)
          {
            Assimp.Mesh mesh1 = input.Meshes[meshIndex];
            VRageRender.Import.Mesh mesh2 = this.m_meshes[this.m_sourceMeshMap[meshIndex]];
            int vertexOffset = mesh2.VertexOffset;
            MyDebug.Assert(mesh1.GetIndices().Length % 3 == 0, file: "E:\\Repo3\\Sources\\Tools\\MwmBuilder\\MyModelProcessor.cs", line: 1664);
            MyMeshSectionMeshInfo meshSectionMeshInfo = new MyMeshSectionMeshInfo()
            {
              MaterialName = input.Materials[mesh1.MaterialIndex].Name,
              IndexCount = mesh2.IndexCount,
              StartIndex = mesh2.StartIndex
            };
            myMeshSectionInfo.Meshes.Add(meshSectionMeshInfo);
          }
          this.m_sections.Add(myMeshSectionInfo);
        }
      }
    }

    private GImpactQuantizedBvh CalculateBvh()
    {
      this.m_bvhIndices.Clear();
      foreach (KeyValuePair<int, MyMeshPartInfo> keyValuePair in this.m_MeshPartSolver.GetMeshPartContainer())
        this.m_bvhIndices.AddRange((IEnumerable<int>) keyValuePair.Value.m_indices);
      GImpactQuantizedBvh gimpactQuantizedBvh = new GImpactQuantizedBvh((IPrimitiveManagerBase) this);
      if (this.m_bvhIndices.Count > 0)
        gimpactQuantizedBvh.BuildSet();
      return gimpactQuantizedBvh;
    }

    private void ReportDegeneratedTriangles(IMyBuildLogger logger, string filename)
    {
      if (this.m_packedVertices.Count <= 0)
        return;
      int[] array = this.m_MeshPartSolver.GetMeshPartContainer().SelectMany<KeyValuePair<int, MyMeshPartInfo>, int>((Func<KeyValuePair<int, MyMeshPartInfo>, IEnumerable<int>>) (x => (IEnumerable<int>) x.Value.m_indices)).ToArray<int>();
      for (int index = 0; index < array.Length; index += 3)
      {
        Vector3 vertex0 = VF_Packer.UnpackPosition(this.m_packedVertices[array[index]]);
        Vector3 vector3_1 = VF_Packer.UnpackPosition(this.m_packedVertices[array[index + 1]]);
        Vector3 vector3_2 = VF_Packer.UnpackPosition(this.m_packedVertices[array[index + 2]]);
        Vector3 vertex1 = vector3_1;
        Vector3 vertex2 = vector3_2;
        if (MyUtils.IsWrongTriangle(vertex0, vertex1, vertex2))
          logger.LogMessage(MessageType.Error, "Model contains degenerated triangle (indices " + (object) array[index] + " " + (object) array[index + 1] + " " + (object) array[index + 2] + ")", filename);
      }
    }
  }
}
