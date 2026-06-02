namespace DisplayBlackout.Platform;

internal interface IAppIndicator : IDisposable
{
    event Action? Clicked;

    event Action? DoubleClicked;

    /// <summary>Fired instead of exiting directly — subscriber decides whether to proceed.</summary>
    event Action? ExitRequested;

    void Show();

    void SetActive(bool isActive);
}
