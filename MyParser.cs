// Decompiled with JetBrains decompiler
// Type: MwmBuilder.MyParser
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using System;
using System.Globalization;

namespace MwmBuilder
{
  public class MyParser
  {
    public static T Parse<T>(object value) => typeof (T).IsEnum ? (T) Enum.Parse(typeof (T), (string) value) : (T) Convert.ChangeType(value, typeof (T), (IFormatProvider) CultureInfo.InvariantCulture);
  }
}
