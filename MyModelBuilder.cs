// Decompiled with JetBrains decompiler
// Type: MwmBuilder.MyModelBuilder
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using Assimp;
using Assimp.Configs;
using MwmBuilder.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using VRageMath;
using VRageRender.Import;

namespace MwmBuilder
{
    public class MyModelBuilder
    {
        private static readonly string LOD_POSTFIX = "_LOD";
        private Dictionary<string, Action<MyModelProcessor, object>> m_setters;

        public MyModelBuilder() => InitSetters();

        private void InitSetters()
        {
            m_setters = new Dictionary<string, Action<MyModelProcessor, object>>();
            foreach (PropertyInfo property in typeof(MyModelProcessor).GetProperties())
            {
                if (property.GetCustomAttributes(typeof(BrowsableAttribute), true).OfType<BrowsableAttribute>().Any<BrowsableAttribute>(s => s.Browsable))
                    m_setters.Add(property.Name, MyModelBuilder.GetValueSetter<MyModelProcessor, object>(property));
            }
        }

        private static Action<T1, T2> GetValueSetter<T1, T2>(PropertyInfo propertyInfo)
        {
            MethodInfo method = typeof(MyParser).GetMethod("Parse", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(propertyInfo.PropertyType);
            ParameterExpression parameterExpression1 = Expression.Parameter(typeof(T1), "i");
            ParameterExpression parameterExpression2 = Expression.Parameter(typeof(T2), "a");
            return Expression.Lambda<Action<T1, T2>>(Expression.Call(
                parameterExpression1,
                propertyInfo.GetSetMethod(), (Expression)Expression.Convert(
                parameterExpression2,
                propertyInfo.PropertyType, method)), parameterExpression1, parameterExpression2).Compile();
        }

        public void Build(
          string filename,
          string intermediateDir,
          string outputDir,
          MyModelConfiguration configuration,
          byte[] havokCollisionShapes,
          bool checkOpenBoundaries,
          float[] lodDistances,
          bool overrideLods,
          bool genSharedStub,
          Func<string, MyMaterialConfiguration> getMaterialByRef,
          IMyBuildLogger logger)
        {
            string withoutExtension = Path.GetFileNameWithoutExtension(filename);
            string directoryName = Path.GetDirectoryName(filename);
            string str1 = "content";
            int num1 = directoryName.ToLower().LastIndexOf(str1) + str1.Length + 1;
            string path1 = directoryName.Substring(num1, directoryName.Length - num1);
            directoryName.Substring(0, num1);
            Path.Combine(path1, withoutExtension + ".FBX");
            AssimpContext assimpContext = new AssimpContext();
            assimpContext.SetConfig(new NormalSmoothingAngleConfig(66f));
            assimpContext.SetConfig(new FBXPreservePivotsConfig(false));
            Scene input = assimpContext.ImportFile(filename, PostProcessSteps.CalculateTangentSpace | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.Triangulate | PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.SplitLargeMeshes | PostProcessSteps.LimitBoneWeights | PostProcessSteps.SortByPrimitiveType | PostProcessSteps.FindInvalidData | PostProcessSteps.GenerateUVCoords | PostProcessSteps.FlipWindingOrder);
            string outputDir1 = outputDir;
            if (input.MeshCount == 0 && input.AnimationCount == 0)
                throw new Exception("Number of meshes is 0 and no animation present!");
            if (input.MaterialCount > 0)
            {
                List<MyMaterialConfiguration> materialConfigurationList = new List<MyMaterialConfiguration>();
                for (int index = 0; index < input.MaterialCount; ++index)
                {
                    MyMaterialConfiguration materialConfiguration = getMaterialByRef(input.Materials[index].Name);
                    if (materialConfiguration != null)
                        materialConfigurationList.Add(materialConfiguration);
                }
                if (materialConfigurationList.Count > 0)
                    configuration.Materials = configuration.Materials != null ? ((IEnumerable<MyMaterialConfiguration>)configuration.Materials).Union<MyMaterialConfiguration>(materialConfigurationList.ToArray()).ToArray<MyMaterialConfiguration>() : materialConfigurationList.ToArray();
            }
            MyModelProcessor processor = CreateProcessor(configuration);
            if (configuration.Materials != null)
            {
                foreach (MyMaterialConfiguration material in configuration.Materials)
                {
                    try
                    {
                        Dictionary<string, object> dictionary = new Dictionary<string, object>();
                        if (processor.MaterialProperties.Keys.Contains<string>(material.Name))
                        {
                            logger.LogMessage(MessageType.Error, "Material: " + material.Name + " is already defined in the processor. Not adding it again..", filename);
                        }
                        else
                        {
                            processor.MaterialProperties.Add(material.Name, dictionary);
                            foreach (MyModelParameter parameter in material.Parameters)
                                dictionary.Add(parameter.Name, parameter.Value);
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogMessage(MessageType.Error, "Problem when procesing materials: " + ex.Message, filename);
                    }
                }
            }
            int num2 = 999;
            List<MyLODDescriptor> myLodDescriptorList = new List<MyLODDescriptor>();
            for (int index = 0; index < num2; ++index)
            {
                string path = Path.Combine(directoryName, withoutExtension + MyModelBuilder.LOD_POSTFIX + (index + 1)) + ".fbx";
                string str2 = Path.Combine(path1, withoutExtension + MyModelBuilder.LOD_POSTFIX + (index + 1));
                if (File.Exists(path))
                {
                    if (overrideLods && lodDistances != null && (index < lodDistances.Length && lodDistances[index] > 0.0))
                    {
                        MyLODDescriptor myLodDescriptor = new MyLODDescriptor()
                        {
                            Distance = lodDistances[index],
                            Model = str2
                        };
                        myLodDescriptorList.Add(myLodDescriptor);
                    }
                    else if (configuration.LODs != null && index < configuration.LODs.Length)
                    {
                        MyLODConfiguration loD = configuration.LODs[index];
                        MyLODDescriptor myLodDescriptor = new MyLODDescriptor()
                        {
                            Distance = loD.Distance,
                            Model = configuration.LODs[index].Model,  //str2,   <-- FIx for LODS. Take defined path in the config.,
                            RenderQuality = loD.RenderQuality
                        };
                        if (str2.ToLower() != loD.Model.ToLower())
                            logger.LogMessage(MessageType.Error, "LOD" + (index + 1) + " name differs " + str2 + " and " + loD.Model, filename);
                        myLodDescriptorList.Add(myLodDescriptor);
                    }
                    else
                        logger.LogMessage(MessageType.Error, "LOD" + (index + 1) + " model exists but configuration is missing", filename);
                }
                else if (configuration.LODs != null && index < configuration.LODs.Length)
                    logger.LogMessage(MessageType.Error, "LOD model " + configuration.LODs[index].Model + " is missing", filename);
                else
                    break;
            }
            processor.LODs = myLodDescriptorList.ToArray();
            processor.BoneGridMapping = configuration.BoneGridSize;
            processor.BoneMapping = configuration.BoneMapping != null ? ((IEnumerable<MyModelVector>)configuration.BoneMapping).Select<MyModelVector, Vector3>(s => new Vector3(s.X, s.Y, s.Z)).ToArray<Vector3>() : null;
            processor.HavokCollisionShapes = havokCollisionShapes;
            if (genSharedStub && myLodDescriptorList.Count != 0)
            {
                string path2 = withoutExtension + MyModelBuilder.LOD_POSTFIX + 0;
                string outputFilename = Path.Combine(directoryName, path2);
                string lod0GeomPath = Path.Combine(path1, path2);
                processor.Process(input, outputFilename, outputDir1, filename, checkOpenBoundaries, logger);
                processor.ExportSharedStub(filename, outputDir1, lod0GeomPath, logger);
            }
            else
                processor.Process(input, filename, outputDir1, filename, checkOpenBoundaries, logger);
            if (configuration.BoneGridSize.HasValue)
                configuration.BoneMapping = ((IEnumerable<Vector3>)processor.BoneMapping).Select<Vector3, MyModelVector>(s => s).ToArray<MyModelVector>();
            List<MyMaterialConfiguration> materialConfigurationList1 = new List<MyMaterialConfiguration>();
            foreach (KeyValuePair<string, Dictionary<string, object>> materialProperty in processor.MaterialProperties)
                materialConfigurationList1.Add(new MyMaterialConfiguration()
                {
                    Name = materialProperty.Key,
                    Parameters = MyModelBuilder.GetParameters(materialProperty)
                });
            configuration.Materials = materialConfigurationList1.Count <= 0 ? null : materialConfigurationList1.ToArray();
            if (processor.LODs == null)
                return;
            List<MyLODConfiguration> lodConfigurationList = new List<MyLODConfiguration>();
            foreach (MyLODDescriptor loD in processor.LODs)
                lodConfigurationList.Add(new MyLODConfiguration()
                {
                    Distance = loD.Distance,
                    Model = loD.Model,
                    RenderQuality = loD.RenderQuality
                });
            configuration.LODs = lodConfigurationList.ToArray();
        }

        private MyModelProcessor CreateProcessor(MyModelConfiguration configuration)
        {
            MyModelProcessor myModelProcessor = new MyModelProcessor();
            foreach (MyModelParameter parameter in configuration.Parameters)
            {
                Action<MyModelProcessor, object> action;
                if (m_setters.TryGetValue(parameter.Name, out action))
                    action(myModelProcessor, parameter.Value);
            }
            return myModelProcessor;
        }

        private static MyModelParameter[] GetParameters(
          KeyValuePair<string, Dictionary<string, object>> materialProps)
        {
            return materialProps.Value.Select<KeyValuePair<string, object>, MyModelParameter>(s => new MyModelParameter()
            {
                Name = s.Key,
                Value = s.Value != null ? s.Value.ToString() : ""
            }).ToArray<MyModelParameter>();
        }

        public static string RemoveSpecialCharacters(string str) => Regex.Replace(str, "[^a-zA-Z0-9_.:,;]+", "", RegexOptions.Compiled);
    }
}
