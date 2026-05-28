namespace DisplayBlackout.Platform;

internal sealed class NullDisplayPowerService : IDisplayPowerService
{
    public static readonly NullDisplayPowerService Instance = new();

    public bool CanSleep(string displayId) => false;
    public bool Sleep(string displayId) => false;
    public bool Wake(string displayId) => false;
    public bool IsAsleep(string displayId) => false;
}
