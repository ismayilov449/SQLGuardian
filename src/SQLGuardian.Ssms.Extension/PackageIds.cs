using System;

namespace SQLGuardian.Ssms.Extension;

internal static class PackageGuids
{
    public const string PackageString = "a3f7c2d1-8e4b-4f9a-9c1e-2b6d0a5e7f31";
    public static readonly Guid Package = new(PackageString);

    public const string CommandSetString = "b8e1d4a2-5c7f-4e3b-9a0d-6f2c1b8e4d90";
    public static readonly Guid CommandSet = new(CommandSetString);
}

internal static class PackageIds
{
    public const int SQLGuardianToolsGroup = 0x1200;
    public const int AnalyzeActiveDocumentCommand = 0x0100;
    public const int ClearErrorListCommand = 0x0101;
}
