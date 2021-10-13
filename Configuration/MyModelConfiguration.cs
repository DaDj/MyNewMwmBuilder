// Decompiled with JetBrains decompiler
// Type: MwmBuilder.Configuration.MyModelConfiguration
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using System.Xml.Serialization;

namespace MwmBuilder.Configuration
{
  [XmlRoot("Model")]
  public class MyModelConfiguration
  {
    [XmlAttribute("Name")]
    public string Name;
    [XmlElement("BoneGridSize")]
    public float? BoneGridSize;
    [XmlArray("BoneMapping")]
    [XmlArrayItem("Bone")]
    public MyModelVector[] BoneMapping;
    [XmlElement("Parameter")]
    public MyModelParameter[] Parameters;
    [XmlElement("Material")]
    public MyMaterialConfiguration[] Materials;
    [XmlElement("MaterialRef")]
    public MyModelParameter[] MaterialRefs;
    [XmlElement("LOD")]
    public MyLODConfiguration[] LODs;

    public bool ShouldSerializeBoneGridSize() => this.BoneGridSize.HasValue;

    public bool ShouldSerializeBoneMapping() => this.BoneMapping != null;
  }
}
