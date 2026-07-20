using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Beacon.WinUI;

internal sealed class ThinDesktopAcrylicBackdrop : SystemBackdrop, IDisposable
{
    private DesktopAcrylicController? _controller;

    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (_controller is not null)
        {
            throw new InvalidOperationException("This backdrop cannot be shared.");
        }

        var controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
        try
        {
            controller.SetSystemBackdropConfiguration(
                GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot));
            controller.AddSystemBackdropTarget(connectedTarget);
            _controller = controller;
        }
        catch
        {
            controller.Dispose();
            throw;
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        _controller?.RemoveSystemBackdropTarget(disconnectedTarget);
        Dispose();
    }

    public void Dispose()
    {
        _controller?.Dispose();
        _controller = null;
        GC.SuppressFinalize(this);
    }
}
