// Decompiled with JetBrains decompiler
// Type: MwmBuilder.Configuration.MyModelParameter
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using System.Xml.Serialization;

namespace MwmBuilder.Configuration
{
  public class MyModelParameter
  {
    [XmlAttribute("Name")]
    public string Name;
    [XmlText]
    public string Value;

    public override int GetHashCode() => this.Name.GetHashCode() * (!string.IsNullOrEmpty(this.Value) ? this.Value.GetHashCode() : 1);
  }
}
