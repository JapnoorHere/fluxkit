using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace FluxKit.Platform.Shell;

/// <summary>Whether FluxKit can get its package identity, and if not, why.</summary>
public enum IdentityState
{
    /// <summary>FluxKit is running with its identity: notifications can be read.</summary>
    Active,

    /// <summary>The identity package is registered, but this process started before that: restart FluxKit.</summary>
    NeedsRestart,

    /// <summary>Not registered yet; <see cref="PackageIdentity.RegisterAsync"/> can do it.</summary>
    NotRegistered,

    /// <summary>It can't be registered here: no signed package ships with this build and Developer Mode is off.</summary>
    Unavailable,
}

/// <summary>
/// FluxKit's identity package (ADR-014): a sparse package that gives the unpackaged FluxKit.exe the identity Windows
/// requires before it may read notifications. It is registered for the current user only, needs no admin rights,
/// and can be removed again. A signed <c>FluxKit.Identity.msix</c> next to the app is used when present; otherwise,
/// in Developer Mode, the unsigned manifest is registered.
/// </summary>
public static class PackageIdentity
{
    public const string PackageName = "FluxKit.Identity";

    private const string IdentityFolder = "Identity";
    private const string SignedPackage = "FluxKit.Identity.msix";
    private const string Manifest = "AppxManifest.xml";

    /// <summary>True when this process is running with a package identity.</summary>
    public static bool IsActive
    {
        get
        {
            try
            {
                return Package.Current is not null;
            }
            catch (InvalidOperationException)
            {
                return false; // No identity.
            }
        }
    }

    public static IdentityState GetState(string appFolder)
    {
        if (IsActive)
        {
            return IdentityState.Active;
        }

        if (IsRegistered())
        {
            return IdentityState.NeedsRestart;
        }

        return File.Exists(Path.Combine(appFolder, IdentityFolder, SignedPackage)) || (DeveloperMode && File.Exists(Path.Combine(appFolder, IdentityFolder, Manifest)))
            ? IdentityState.NotRegistered
            : IdentityState.Unavailable;
    }

    /// <summary>
    /// Registers the identity package for the current user, pointing it at <paramref name="appFolder"/>. FluxKit has to
    /// restart afterwards for the identity to apply. Returns an error message, or null on success.
    /// </summary>
    public static async Task<string?> RegisterAsync(string appFolder)
    {
        var manager = new PackageManager();
        var external = new Uri(appFolder.EndsWith(Path.DirectorySeparatorChar) ? appFolder : appFolder + Path.DirectorySeparatorChar);
        string signed = Path.Combine(appFolder, IdentityFolder, SignedPackage);
        string manifest = Path.Combine(appFolder, IdentityFolder, Manifest);
        try
        {
            DeploymentResult result = File.Exists(signed)
                ? await manager.AddPackageByUriAsync(new Uri(signed), new AddPackageOptions { ExternalLocationUri = external })
                : await manager.RegisterPackageByUriAsync(new Uri(manifest), new RegisterPackageOptions { ExternalLocationUri = external, DeveloperMode = true });
            return result.IsRegistered ? null : result.ErrorText;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or FileNotFoundException)
        {
            return ex.Message;
        }
    }

    /// <summary>Removes the identity package for the current user (for "reset" and uninstall).</summary>
    public static async Task UnregisterAsync()
    {
        var manager = new PackageManager();
        foreach (Package package in manager.FindPackagesForUser(string.Empty).Where(p => p.Id.Name == PackageName))
        {
            await manager.RemovePackageAsync(package.Id.FullName);
        }
    }

    private static bool IsRegistered()
    {
        try
        {
            return new PackageManager().FindPackagesForUser(string.Empty).Any(p => p.Id.Name == PackageName);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool DeveloperMode =>
        Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", "AllowDevelopmentWithoutDevLicense", 0) is int on && on == 1;
}
