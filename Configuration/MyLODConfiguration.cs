// Decompiled with JetBrains decompiler
// Type: MwmBuilder.Configuration.MyLODConfiguration
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using System.Xml.Serialization;

namespace MwmBuilder.Configuration
{
  [XmlRoot("LOD")]
  public class MyLODConfiguration
  {
    [XmlAttribute("Distance")]
    public float Distance;
    [XmlAttribute("RenderQuality")]
    public string RenderQuality;
    [XmlElement("Model")]
    public string Model;

    public override int GetHashCode()
    {
      int hashCode = this.Distance.GetHashCode();
      int num = hashCode | hashCode * 397 ^ (this.RenderQuality == null ? "".GetHashCode() : this.RenderQuality.GetHashCode());
      return num | num * 397 ^ (this.Model == null ? "".GetHashCode() : this.Model.GetHashCode());
    }
  }
}
