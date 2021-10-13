// Decompiled with JetBrains decompiler
// Type: MwmBuilder.ContentBuildLogger
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

namespace MwmBuilder
{
  internal abstract class ContentBuildLogger
  {
    public abstract void LogImportantMessage(string message, params object[] messageArgs);

    public abstract void LogMessage(string message, params object[] messageArgs);

    public abstract void LogWarning(string helpLink, string message, params object[] messageArgs);
  }
}
