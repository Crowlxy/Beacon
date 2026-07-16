# Portable distribution

## Primary artifact

```text
Beacon-Portable-x64.zip
└─ Beacon/
   ├─ Beacon.exe
   ├─ Beacon.PluginHost.exe
   ├─ runtimes/
   ├─ Plugins/
   ├─ Data/
   ├─ LICENSE
   ├─ attribution.md
   └─ portable.flag
```

The target deployment model is **Unpackaged + self-contained Windows App SDK**. Users should not need an installer, Store account, administrator permission, or a separately installed Windows App SDK runtime.

## DataRoot

Persistent data belongs under `<BeaconRoot>\Data` in portable mode. Beacon and all helper processes receive the same normalized absolute DataRoot. They must not independently select storage locations.

Expected subdirectories:

```text
Data/
├─ Settings/
├─ History/
├─ Plugins/
├─ Cache/
├─ Logs/
├─ Clipboard/
└─ State/
```

If the application directory is not writable, Beacon must explain the problem and suggest moving the folder. It must not silently switch to AppData and pretend to remain portable.

## Windows integration

Startup, shortcuts, URI/file associations, notifications, and Explorer integration are optional actions initiated by the user. Every registration must have a corresponding removal or repair action, especially after the folder is moved.

## Updates

The first technical spike must compare manual ZIP replacement with a separate updater process. Any updater must:

- Verify artifact integrity
- Preserve `Data`, plugins, and user files
- Replace locked files only after Beacon exits
- Keep a rollback copy
- Work offline when no update is requested
- Version its update manifest

Automatic updates are not required for the first MVP.

## Optional packaged distribution

MSIX and Microsoft Store support belong to Phase R11. They must not become prerequisites for the portable release or force package-specific assumptions into Core and Contracts.