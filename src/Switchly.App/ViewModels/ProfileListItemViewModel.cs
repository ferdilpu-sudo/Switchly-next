using Switchly.App.Common;
using Switchly.Domain.Profiles;

namespace Switchly.App.ViewModels;

/// <summary>Display projection for a single BrowserProfile row in the Main Workspace.</summary>
public sealed class ProfileListItemViewModel : ViewModelBase
{
    private ProfileRuntimeState _runtimeState;
    private int _openWindowCount;
    private bool _isCurrentlyDocked;

    public ProfileListItemViewModel(
        BrowserProfile profile,
        ProfileRuntimeState runtimeState,
        int openWindowCount,
        bool isCurrentlyDocked = false,
        string? manualGroupName = null)
    {
        Profile = profile;
        _runtimeState = runtimeState;
        _openWindowCount = openWindowCount;
        _isCurrentlyDocked = isCurrentlyDocked;
        ManualGroupName = manualGroupName;
    }

    public string? ManualGroupName { get; }

    public bool HasManualGroup => !string.IsNullOrWhiteSpace(ManualGroupName);

    public string GroupBadgeLabel => string.IsNullOrWhiteSpace(ManualGroupName) ? "Tanpa Grup" : ManualGroupName;

    public BrowserProfile Profile { get; }

    public Guid ProfileId => Profile.ProfileId;

    public string Name => Profile.Name;

    public string InitialLetter => string.IsNullOrWhiteSpace(Profile.Name)
        ? "?"
        : Profile.Name.Trim()[0].ToString().ToUpperInvariant();

    public string StartupUrlDisplay => string.IsNullOrWhiteSpace(Profile.StartupUrl) ? "Halaman kosong (about:blank)" : Profile.StartupUrl;

    public string DomainLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Profile.StartupUrl))
            {
                return "Umum";
            }

            if (Uri.TryCreate(Profile.StartupUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                    ? uri.Host[4..]
                    : uri.Host;
            }

            return Profile.StartupUrl;
        }
    }

    public bool IsCurrentlyDocked
    {
        get => _isCurrentlyDocked;
        set => SetProperty(ref _isCurrentlyDocked, value);
    }

    public string ColorHex => string.IsNullOrWhiteSpace(Profile.ColorHex) ? "#1B6EF3" : Profile.ColorHex;

    public bool IsFavorite => Profile.IsFavorite;

    public string FavoriteGlyph => Profile.IsFavorite ? "★" : "☆";

    public bool IsArchived => Profile.IsArchived;

    public ProfileRuntimeState RuntimeState
    {
        get => _runtimeState;
        set
        {
            if (SetProperty(ref _runtimeState, value))
            {
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    public int OpenWindowCount
    {
        get => _openWindowCount;
        set
        {
            if (SetProperty(ref _openWindowCount, value))
            {
                OnPropertyChanged(nameof(StatusLabel));
            }
        }
    }

    public bool IsActive => RuntimeState == ProfileRuntimeState.Active;

    public string StatusLabel => RuntimeState switch
    {
        ProfileRuntimeState.Active => OpenWindowCount > 1 ? $"Aktif ({OpenWindowCount} jendela)" : "Aktif",
        ProfileRuntimeState.Starting => "Membuka...",
        ProfileRuntimeState.Suspending => "Menutup...",
        ProfileRuntimeState.Suspended => "Suspended",
        ProfileRuntimeState.Error => "Error",
        _ => "Tertutup",
    };

    public string StatusColor => RuntimeState switch
    {
        ProfileRuntimeState.Active => "#16A34A",
        ProfileRuntimeState.Starting or ProfileRuntimeState.Suspending => "#D97706",
        ProfileRuntimeState.Error => "#DC2626",
        _ => "#6B7280",
    };
}
