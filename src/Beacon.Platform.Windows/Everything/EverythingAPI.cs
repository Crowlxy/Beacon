// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/EverythingAPI.cs (Flow Launcher/Wox, MIT).
using System.Runtime.CompilerServices;

namespace Beacon.Platform.Windows.Everything;

public static class EverythingApi
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public enum StateCode { Ok, MemoryError, IpcError, RegisterClassExError, CreateWindowError, CreateThreadError, InvalidIndexError, InvalidCallError }

    public static async ValueTask<(bool Available, string? Reason)> GetAvailabilityAsync(CancellationToken token)
    {
        try
        {
            await Gate.WaitAsync(token);
            try
            {
                _ = EverythingApiDllImport.GetMajorVersion();
                return EverythingApiDllImport.GetLastError() == StateCode.IpcError
                    ? (false, "Everything service is not running.")
                    : (true, null);
            }
            finally { Gate.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return (false, "Search was cancelled."); }
        catch (DllNotFoundException) { return (false, "Everything.dll is unavailable."); }
        catch (BadImageFormatException) { return (false, "Everything.dll is not the x64 SDK binary."); }
        catch (EntryPointNotFoundException) { return (false, "Everything.dll is incompatible."); }
    }

    public static async IAsyncEnumerable<(string Path, bool IsFolder)> SearchAsync(
        EverythingSearchOption option,
        [EnumeratorCancellation] CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(option.Offset);
        ArgumentOutOfRangeException.ThrowIfNegative(option.MaxCount);
        await Gate.WaitAsync(token);
        try
        {
            EverythingApiDllImport.SetSearch(option.Keyword);
            EverythingApiDllImport.SetOffset(option.Offset);
            EverythingApiDllImport.SetMax(option.MaxCount);
            EverythingApiDllImport.SetSort(option.SortOption);
            EverythingApiDllImport.SetMatchPath(option.IsFullPathSearch);
            if (!EverythingApiDllImport.Query(true)) yield break;

            var buffer = new char[32768];
            for (var index = 0; index < EverythingApiDllImport.GetNumResults(); index++)
            {
                token.ThrowIfCancellationRequested();
                string path;
                unsafe
                {
                    fixed (char* pointer = buffer)
                    {
                        EverythingApiDllImport.GetResultFullPathName(index, pointer, buffer.Length);
                        path = new string(pointer);
                    }
                }
                yield return (path, EverythingApiDllImport.IsFolderResult(index));
            }
        }
        finally
        {
            EverythingApiDllImport.Reset();
            Gate.Release();
        }
    }
}
