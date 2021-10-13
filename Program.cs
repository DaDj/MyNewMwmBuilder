// Decompiled with JetBrains decompiler
// Type: MwmBuilder.Program
// Assembly: MwmBuilder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D57E2547-9D5E-4BCB-B023-8717191D3CCC
// Assembly location: E:\Steam\SteamApps\common\SpaceEngineersModSDK\Tools\VRageEditor\Plugins\ModelBuilder\MwmBuilder.exe

namespace MwmBuilder
{
  internal class Program
  {
    public static int Main(string[] args)
    {
      MyConsoleLogger myConsoleLogger = new MyConsoleLogger();
      return new ProgramContext().Work((object) args, new IMyBuildLogger[1]
      {
        (IMyBuildLogger) myConsoleLogger
      });
    }
  }
}
