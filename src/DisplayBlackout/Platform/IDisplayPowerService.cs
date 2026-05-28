namespace DisplayBlackout.Platform;

internal interface IDisplayPowerService
{
    bool CanSleep(string displayId);

    bool Sleep(string displayId);

    bool Wake(string displayId);

    bool IsAsleep(string displayId);
}
