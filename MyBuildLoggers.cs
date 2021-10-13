// Decompiled with JetBrains decompiler
// Type: MwmBuilder.MyBuildLoggers
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

using System.Collections.Generic;

namespace MwmBuilder
{
  internal class MyBuildLoggers : IMyBuildLogger
  {
    private static HashSet<IMyBuildLogger> m_loggers = new HashSet<IMyBuildLogger>();

    public void AddLogger(IMyBuildLogger logger) => MyBuildLoggers.m_loggers.Add(logger);

    public void RemoveLogger(IMyBuildLogger logger) => MyBuildLoggers.m_loggers.Remove(logger);

    public void LogMessage(MessageType messageType, string message, string filename = "")
    {
      foreach (IMyBuildLogger logger in MyBuildLoggers.m_loggers)
        logger.LogMessage(messageType, message, filename);
    }

    public void Close()
    {
      foreach (IMyBuildLogger logger in MyBuildLoggers.m_loggers)
        logger.Close();
    }
  }
}
