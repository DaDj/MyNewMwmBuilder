// Decompiled with JetBrains decompiler
// Type: MwmBuilder.Configuration.MyModelVector
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using System;
using System.Xml.Serialization;
using VRageMath;

namespace MwmBuilder.Configuration
{
  public struct MyModelVector
  {
    [XmlAttribute("X")]
    public int X;
    [XmlAttribute("Y")]
    public int Y;
    [XmlAttribute("Z")]
    public int Z;

    public static implicit operator Vector3(MyModelVector vec) => new Vector3((float) vec.X, (float) vec.Y, (float) vec.Z);

    public static implicit operator MyModelVector(Vector3 vec) => new MyModelVector()
    {
      X = (int) Math.Round((double) vec.X),
      Y = (int) Math.Round((double) vec.Y),
      Z = (int) Math.Round((double) vec.Z)
    };
  }
}
