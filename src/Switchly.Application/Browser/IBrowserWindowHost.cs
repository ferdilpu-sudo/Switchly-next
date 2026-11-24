namespace Switchly.Application.Browser;

/// <summary>Hosts a browser window/tab bound to a profile context.</summary>
public interface IBrowserWindowHost : IDisposable
{
    void Navigate(string url);

    string? Address { get; }

    event Action<string>? AddressChanged;
}
