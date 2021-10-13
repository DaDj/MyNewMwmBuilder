// Decompiled with JetBrains decompiler
// Type: MwmBuilder.Configuration.MyMaterialConfiguration
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using System.Xml.Serialization;

namespace MwmBuilder.Configuration
{
  [XmlRoot("Material")]
  public class MyMaterialConfiguration
  {
    [XmlAttribute("Name")]
    public string Name;
    [XmlElement("Parameter")]
    public MyModelParameter[] Parameters;

    public override int GetHashCode()
    {
      int hashCode = this.Name.GetHashCode();
      foreach (MyModelParameter parameter in this.Parameters)
        hashCode += parameter.GetHashCode();
      return hashCode;
    }
  }
}
