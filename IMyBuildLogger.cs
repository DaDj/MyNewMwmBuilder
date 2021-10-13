// Decompiled with JetBrains decompiler
// Type: MwmBuilder.IMyBuildLogger
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

namespace MwmBuilder
{
  public interface IMyBuildLogger
  {
    void LogMessage(MessageType messageType, string message, string filename = "");

    void Close();
  }
}
