// Decompiled with JetBrains decompiler
// Type: MwmBuilder.ProgramContext
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using MwmBuilder.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using VRage;
using VRage.Collections;

namespace MwmBuilder
{
    public class ProgramContext
    {
        private MyProgramArgs m_args = new MyProgramArgs();
        private static Dictionary<string, object> m_vars = new Dictionary<string, object>()
    {
      {
        "RescaleFactor",
         "0.01"
      }
    };
        private static MyBuildLoggers m_logger = new MyBuildLoggers();
        private static MyFileLogger m_fileLogger = new MyFileLogger();
        private int m_processed;
        private int m_count;
        private DateTime? appLastChangeDateTime;
        public static MyConcurrentQueue<string> Names = new MyConcurrentQueue<string>();
        private Stopwatch m_watch;
        private static List<MyMaterialsLib> m_materialLibs;
        private static string m_materialsPath = "C:\\KeenSWH\\Sandbox\\MediaBuild\\MEContent\\Materials";
        public static string OutputDir;

        public static Dictionary<string, object> DefaultModelParameters => ProgramContext.m_vars;

        public int Work(object data, IMyBuildLogger[] loggers = null)
        {
            if (loggers != null)
            {
                foreach (IMyBuildLogger logger in loggers)
                    ProgramContext.m_logger.AddLogger(logger);
            }
            ProgramContext.m_logger.AddLogger(m_fileLogger);
            m_watch = new Stopwatch();
            m_watch.Start();
            m_args.RegisterArg("s", "SOURCE", null, "Path to source FBX file(s), directory or file. Directories are read recursively. Defaults to current directory.");
            m_args.RegisterArg("o", "OUTPUT", "C:\\Program Files (x86)\\Steam\\SteamApps\\common\\SpaceEngineers\\Content\\", "Path to output");
            m_args.RegisterArg("m", "MASK", "*.fbx", "File mask of files to process, defaults to *.FBX");
            m_args.RegisterArg("l", "LOGFILE", null, "Path to logfile");
            m_args.RegisterArg("la", "LOGFILE", null, "Path to logfile");
            m_args.RegisterArg("t", "THREADS", "1", "Run model build on several threads");
            m_args.RegisterArg("e", null, null, "Force XML export");
            m_args.RegisterArg("a", null, null, "Split logfile to separate errors and warnings to separate logfiles .warn log and .err log");
            m_args.RegisterArg("u", null, null, "Log file when file is up to date");
            m_args.RegisterArg("f", null, null, "Rebuild files even when up-to-date");
            m_args.RegisterArg("g", null, null, "Don't compare app build date to files");
            m_args.RegisterArg("checkOpenBoundaries", null, null, "Warn if model contains open boundaries");
            m_args.RegisterArg("p", null, null, "Wait for key after build");
            m_args.RegisterArg("i", null, null, null);
            m_args.RegisterArg("c", null, null, null);
            m_args.RegisterArg("d", "LODS", null, "Float values separated by space, defining default values for LOD 1-n");
            m_args.RegisterArg("do", "LODSOverride", null, "Float values separated by space, defining overriden values for LOD 1-n");
            m_args.RegisterArg("gss", "SHAREDSTUB", true.ToString(), "Generate shared stub and save LOD0 with postfix _LOD0");
            string[] args = (string[])data;
            try
            {
                m_args.Parse(args);
                if (m_args.Empty)
                {
                    m_args.WriteHelp();
                    return 1;
                }
                if (((IEnumerable<string>)args).FirstOrDefault<string>(a => a.StartsWith("/o")) == null)
                {
                    ProgramContext.m_logger.LogMessage(MessageType.Error, "Output path was not specified!", "");
                    return 1;
                }
                if (!Directory.Exists(m_args.GetValue("o")))
                {
                    ProgramContext.m_logger.LogMessage(MessageType.Error, "Cannot find output path: " + m_args.GetValue("o"), "");
                    return 1;
                }
                if (m_args.GetValue("f") == null && m_args.GetValue("g") == null)
                    appLastChangeDateTime = new DateTime?(File.GetLastWriteTimeUtc(Assembly.GetCallingAssembly().Location));
                if (m_args.GetValue("i") != null)
                {
                    RunAsJobWorker();
                    return 0;
                }
                bool append = m_args.GetValue("la") != null;
                if (m_args.GetValue("l") != null || m_args.GetValue("la") != null)
                {
                    string path = append ? m_args.GetValue("la") : m_args.GetValue("l");
                    ProgramContext.m_fileLogger.InfoLog = new StreamWriter(path, append);
                    if (m_args.GetValue("a") != null)
                    {
                        ProgramContext.m_fileLogger.WarningLog = new StreamWriter(Path.ChangeExtension(path, ".warn" + Path.GetExtension(path)), append);
                        ProgramContext.m_fileLogger.ErrorLog = new StreamWriter(Path.ChangeExtension(path, ".err" + Path.GetExtension(path)), append);
                    }
                }
                string[] strArray = LoadFiles(m_args.GetValue("s"), m_args.GetValue("m"));
                setMaterialsPathForSource(m_args.GetValue("s"));
                ProgramContext.LoadMaterialLibs();
                m_count = strArray.Length;
                if (m_args.GetValue("t") != null)
                {
                    int int32 = Convert.ToInt32(m_args.GetValue("t"));
                    if (int32 > 1)
                    {
                        int num = new MyWorkDispatcher(strArray, m_args.GetValue("o"), null, m_args.GetValue("f") != null, m_args.GetValue("g") != null, m_args.GetValue("e") != null, m_args.GetValue("checkOpenBoundaries") != null, m_args.GetValue("d")).Run(int32, m_args.GetValue("u") != null) ? 0 : 1;
                        ProgramContext.m_logger.Close();
                        WaitForKey();
                        return num;
                    }
                }
                foreach (string file in strArray)
                    ProcessFileSafe(file);
            }
            catch (Exception ex)
            {
                ProgramContext.m_logger.LogMessage(MessageType.Error, ex.ToString(), "");
                WaitForKey();
                return 1;
            }
            ProgramContext.m_logger.Close();
            WaitForKey();
            return 0;
        }

        private void setMaterialsPathForSource(string sourcePath)
        {
            if (sourcePath == null)
                return;
            string[] strArray = sourcePath.Split(';');
            DirectoryInfo directoryInfo = null;
            if (strArray.Length >= 1)
                directoryInfo = Directory.GetParent(strArray[0]);
            for (; directoryInfo != null; directoryInfo = directoryInfo.Parent)
            {
                foreach (DirectoryInfo directory in directoryInfo.GetDirectories())
                {
                    if (directory.Name == "Materials")
                    {
                        ProgramContext.m_materialsPath = directory.FullName;
                        return;
                    }
                }
            }
            ProgramContext.m_logger.LogMessage(MessageType.Error, "Could not find Materials library folder.", "");
        }

        private void ProcessFileSafe(string file)
        {
            try
            {
                bool overrideLods = false;
                if (m_args.GetValue("do") != null)
                    overrideLods = true;
                bool result = true;
                bool.TryParse(m_args.GetValue("gss"), out result);
                if (ProcessFile(file, ProgramContext.OutputDir = m_args.GetValue("o"), ProgramContext.m_vars, m_args.GetValue("e") != null, m_args.GetValue("f") != null, m_args.GetValue("checkOpenBoundaries") != null, overrideLods ? m_args.GetValue("do") : m_args.GetValue("d"), overrideLods, result))
                {
                    lock (ProgramContext.m_logger)
                        ProgramContext.m_logger.LogMessage(MessageType.Processed, string.Format("{1}: Finished in {0}s", m_watch.Elapsed.TotalSeconds.ToString("f1"), m_args.GetValue("i") == null ? "" : m_args.GetValue("i")), file);
                }
                ++m_processed;
            }
            catch (Exception ex)
            {
                ProgramContext.m_logger.LogMessage(MessageType.Error, file + ":" + Environment.NewLine + ex.ToString(), "");
            }
            UpdateProgress(m_processed / (float)m_count);
        }

        private void WaitForKey()
        {
            try
            {
                if (m_args.GetValue("p") == null || Console.IsInputRedirected)
                    return;
                Console.WriteLine("Build done, waiting for key...");
                Console.ReadKey();
            }
            catch
            {
            }
        }

        public void WorkThread(object data) => Work(data);

        public static MyModelConfiguration ImportXml(string xmlFile)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(MyModelConfiguration));
            new XmlSerializerNamespaces().Add(string.Empty, string.Empty);
            using (FileStream fileStream = File.Open(xmlFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return (MyModelConfiguration)xmlSerializer.Deserialize(fileStream);
        }

        private void RunAsJobWorker()
        {
            m_args.GetValue("i");
            while (ProgramContext.Names.Count > 0)
            {
                string instance;
                ProgramContext.Names.TryDequeue(out instance);
                if (!string.IsNullOrEmpty(instance))
                    ProcessFileSafe(instance);
            }
        }

        public static void ExportXml(string xmlFile, MyModelConfiguration configuration)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(MyModelConfiguration));
            XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
            namespaces.Add(string.Empty, string.Empty);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (FileStream fileStream = File.Open(xmlFile, FileMode.OpenOrCreate))
                {
                    xmlSerializer.Serialize(memoryStream, configuration, namespaces);
                    memoryStream.Position = 0L;
                    fileStream.Position = 0L;
                    if (memoryStream.Length == fileStream.Length && ProgramContext.StreamEquals(memoryStream, fileStream))
                        return;
                    fileStream.SetLength(0L);
                    fileStream.Position = 0L;
                    memoryStream.Position = 0L;
                    memoryStream.WriteTo(fileStream);
                }
            }
        }

        private static bool StreamEquals(Stream streamA, Stream streamB)
        {
            byte[] buffer1 = new byte[ushort.MaxValue];
            byte[] buffer2 = new byte[ushort.MaxValue];
            int num1;
            do
            {
                num1 = streamA.Read(buffer1, 0, buffer1.Length);
                int num2 = streamB.Read(buffer2, 0, buffer2.Length);
                if (num1 != num2)
                    return false;
                for (int index = 0; index < num1; ++index)
                {
                    if (buffer1[index] != buffer2[index])
                        return false;
                }
            }
            while (num1 > 0);
            return true;
        }

        private void UpdateProgress(float progress)
        {
            float num = 100f * progress;
            if (num > 100.0)
                num = 100f;
            if (Console.IsInputRedirected)
                return;
            try
            {
                Console.Title = string.Format("Mwm Builder: {0}%", num.ToString("f1"));
            }
            catch
            {
            }
        }

        private string[] LoadFiles(string src, string mask)
        {
            if (src == null)
                src = Directory.GetCurrentDirectory();
            List<string> stringList = new List<string>();
            string str = src;
            char[] chArray = new char[1] { ';' };
            foreach (string path in str.Split(chArray))
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) == 0)
                {
                    if (File.Exists(path))
                        stringList.Add(path);
                    else
                        ProgramContext.m_logger.LogMessage(MessageType.Error, string.Format("Target file '{0}' does not exists", path), "");
                }
                else
                {
                    string[] files = Directory.GetFiles(path, mask, SearchOption.AllDirectories);
                    stringList.AddRange(files);
                }
            }
            return ((IEnumerable<string>)stringList.ToArray()).Select<string, FileInfo>(s => new FileInfo(s)).OrderByDescending<FileInfo, long>(s => s.Length).Select<FileInfo, string>(s => s.FullName).ToArray<string>();
        }

        private bool ProcessFile(
          string file,
          string outputDir,
          Dictionary<string, object> defaultVars,
          bool exportXml,
          bool forceBuild,
          bool checkOpenBoundaries,
          string lodDistances,
          bool overrideLods,
          bool genSharedStub)
        {
            DateTime sourceDateTime = File.GetLastWriteTimeUtc(file);
            string str1 = Path.ChangeExtension(file, "xml");
            bool flag = false;
            MyModelConfiguration configuration = null;
            if (File.Exists(str1))
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(str1);
                if (lastWriteTimeUtc > sourceDateTime)
                    sourceDateTime = lastWriteTimeUtc;
                configuration = ProgramContext.ImportXml(str1);
            }
            else
                flag = exportXml;
            if (configuration == null)
                configuration = new MyModelConfiguration()
                {
                    Name = "Default",
                    Parameters = defaultVars.Select<KeyValuePair<string, object>, MyModelParameter>(s => new MyModelParameter()
                    {
                        Name = s.Key,
                        Value = s.Value.ToString()
                    }).ToArray<MyModelParameter>()
                };
            if (configuration.MaterialRefs != null)
            {
                foreach (MyModelParameter materialRef in configuration.MaterialRefs)
                    ProgramContext.LoadMaterialByRef(materialRef.Name);
            }
            byte[] havokCollisionShapes = ReadExternalFile("hkt", file, ref sourceDateTime);
            if (appLastChangeDateTime.HasValue && appLastChangeDateTime.Value > sourceDateTime)
                sourceDateTime = appLastChangeDateTime.Value;
            FileInfo fileInfo = new FileInfo(MyModelProcessor.GetOutputPath(file, outputDir));
            if (fileInfo.Exists && fileInfo.LastWriteTimeUtc > sourceDateTime && !forceBuild)
            {
                if (flag)
                    ProgramContext.ExportXml(str1, configuration);
                return false;
            }
            ProgramContext.ItemInfo itemInfo = new ProgramContext.ItemInfo()
            {
                Index = 0,
                Path = file,
                Name = Path.GetFileNameWithoutExtension(file),
                Configuration = configuration
            };
            float[] numArray;
            if (lodDistances == null)
                numArray = new float[0];
            else
                numArray = Array.ConvertAll<string, float>(lodDistances.Trim().Split(' '), new Converter<string, float>(float.Parse));
            float[] lodDistances1 = numArray;
            ProcessItem(itemInfo, outputDir, havokCollisionShapes, checkOpenBoundaries, lodDistances1, overrideLods, genSharedStub);
            if (exportXml)
            {
                List<string> materialsToRef = new List<string>();
                foreach (MyMaterialConfiguration material in configuration.Materials)
                {
                    if (ProgramContext.GetMaterialByRef(material.Name) != null)
                        materialsToRef.Add(material.Name);
                }
                if (materialsToRef.Count > 0)
                {
                    configuration.Materials = ((IEnumerable<MyMaterialConfiguration>)configuration.Materials).Where<MyMaterialConfiguration>(x => !materialsToRef.Contains(x.Name)).ToArray<MyMaterialConfiguration>();
                    if (configuration.MaterialRefs == null)
                    {
                        configuration.MaterialRefs = materialsToRef.ConvertAll<MyModelParameter>(x => new MyModelParameter()
                        {
                            Name = x
                        }).ToArray();
                    }
                    else
                    {
                        List<MyModelParameter> myModelParameterList = new List<MyModelParameter>();
                        foreach (string str2 in materialsToRef)
                        {
                            string mat = str2;
                            if (!((IEnumerable<MyModelParameter>)configuration.MaterialRefs).Any<MyModelParameter>(x => x.Name == mat))
                                myModelParameterList.Add(new MyModelParameter()
                                {
                                    Name = mat
                                });
                        }
                        configuration.MaterialRefs = ((IEnumerable<MyModelParameter>)configuration.MaterialRefs).Union<MyModelParameter>(myModelParameterList).ToArray<MyModelParameter>();
                    }
                }
                ProgramContext.ExportXml(str1, configuration);
            }
            return true;
        }

        private void ProcessItem(
          ProgramContext.ItemInfo item,
          string outputDir,
          byte[] havokCollisionShapes,
          bool checkOpenBoundaries,
          float[] lodDistances,
          bool overrideLods,
          bool genSharedStub)
        {
            if (item.Configuration == null)
                ProgramContext.m_logger.LogMessage(MessageType.Info, string.Format("Model skipped! No configuration for '{0}'", item.Path), "");
            else
                new MyModelBuilder().Build(item.Path, "tmp", outputDir, item.Configuration, havokCollisionShapes, checkOpenBoundaries, lodDistances, overrideLods, genSharedStub, new Func<string, MyMaterialConfiguration>(ProgramContext.GetMaterialByRef), m_logger);
        }

        private byte[] ReadExternalFile(string extension, string file, ref DateTime sourceDateTime)
        {
            byte[] buffer = null;
            string path = Path.ChangeExtension(file, extension);
            if (File.Exists(path))
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
                if (lastWriteTimeUtc > sourceDateTime)
                    sourceDateTime = lastWriteTimeUtc;
                FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                long length = fileStream.Length;
                if (length > 0L)
                {
                    buffer = new byte[length];
                    fileStream.Read(buffer, 0, Convert.ToInt32(length));
                }
                fileStream.Close();
            }
            return buffer;
        }

        private void ParseVariables(string vars)
        {
            ProgramContext.m_vars = new Dictionary<string, object>();
            vars = vars.Replace("\"", "");
            string str1 = vars.Substring(3);
            char[] separator1 = new char[1] { ';' };
            foreach (string str2 in str1.Split(separator1, StringSplitOptions.RemoveEmptyEntries))
            {
                char[] separator2 = new char[1] { '=' };
                string[] strArray = str2.Split(separator2, StringSplitOptions.RemoveEmptyEntries);
                if (strArray.Length == 2)
                    ProgramContext.m_vars.Add(strArray[0], strArray[1]);
            }
        }

        public static MyMaterialConfiguration GetMaterialByRef(string materialRef)
        {
            MyDebug.Assert(ProgramContext.m_materialLibs != null, "Material libraries were not loaded!", "E:\\Repo3\\Sources\\Tools\\MwmBuilder\\ProgramContext.cs", 578);
            foreach (MyMaterialsLib materialLib in ProgramContext.m_materialLibs)
            {
                MyMaterialConfiguration materialConfiguration = Array.Find<MyMaterialConfiguration>(materialLib.Materials, e => e.Name == materialRef);
                if (materialConfiguration != null)
                    return materialConfiguration;
            }
            return null;
        }

        public static MyMaterialConfiguration LoadMaterialByRef(
          string materialRef)
        {
            MyMaterialConfiguration materialByRef = ProgramContext.GetMaterialByRef(materialRef);
            if (materialByRef != null)
                return materialByRef;
            ProgramContext.m_logger.LogMessage(MessageType.Error, "Referenced material: " + materialRef + " was not found in material libraries", "");
            return materialByRef;
        }

        public static void LoadMaterialLibs()
        {
            ProgramContext.m_materialLibs = new List<MyMaterialsLib>();
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(MyMaterialsLib));
            new XmlSerializerNamespaces().Add(string.Empty, string.Empty);
            if (Directory.Exists(ProgramContext.m_materialsPath))
            {
                try
                {
                    foreach (string file in Directory.GetFiles(ProgramContext.m_materialsPath))
                    {
                        string xmlFile = file;
                        if (xmlFile.EndsWith(".xml"))
                        {
                            using (FileStream fileStream = File.Open(xmlFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                try
                                {
                                    lock (ProgramContext.m_materialLibs)
                                    {
                                        if (ProgramContext.m_materialLibs.Find(x => x.FilePath == xmlFile) == null)
                                        {
                                            MyMaterialsLib myMaterialsLib = (MyMaterialsLib)xmlSerializer.Deserialize(fileStream);
                                            myMaterialsLib.FilePath = xmlFile;
                                            ProgramContext.m_materialLibs.Add(myMaterialsLib);
                                        }
                                    }
                                }
                                catch
                                {
                                    ProgramContext.m_logger.LogMessage(MessageType.Error, "This xml library: " + xmlFile + " couldn't been loaded. Probably wrong XML format.", "");
                                }
                            }
                        }
                    }
                }
                catch (IOException ex)
                {
                    ProgramContext.m_logger.LogMessage(MessageType.Error, "Libs in " + ProgramContext.m_materialsPath + " couldn't been loaded.. Wrong material library path?", "");
                }
            }
            Trace.Assert(ProgramContext.m_materialLibs != null, "No material libraries were not loaded from path: " + ProgramContext.m_materialsPath + " !");
            if (ProgramContext.m_materialLibs == null)
                return;
            ProgramContext.CheckForMaterialDuplicates();
        }

        public static void CheckForMaterialDuplicates()
        {
            foreach (MyMaterialsLib materialLib1 in ProgramContext.m_materialLibs)
            {
                foreach (MyMaterialConfiguration material1 in materialLib1.Materials)
                {
                    foreach (MyMaterialsLib materialLib2 in ProgramContext.m_materialLibs)
                    {
                        foreach (MyMaterialConfiguration material2 in materialLib2.Materials)
                            Trace.Assert((material1.Equals(material2) ? 1 : (material1.Name != material2.Name ? 1 : 0)) != 0, "Material: " + material1.Name + " from library: " + materialLib1.FilePath + " is duplicated in: " + materialLib2.FilePath);
                    }
                }
            }
        }

        private struct ItemInfo
        {
            public int Index;
            public string Name;
            public string Path;
            public MyModelConfiguration Configuration;
        }
    }
}
