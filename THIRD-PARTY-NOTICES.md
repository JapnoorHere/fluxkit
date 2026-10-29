# Third-party notices

FluxKit is MIT-licensed. It uses or references the following third-party work.

## Runtime components (shipped in the zip)

| Component | License | Use |
|---|---|---|
| .NET runtime | MIT | Application runtime (self-contained) |
| Windows App SDK / WinUI 3 | MIT | UI framework (self-contained) |
| Serilog, Serilog.Sinks.File | Apache-2.0 | Logging |

## References (no code copied verbatim)

The undocumented Windows virtual-desktop COM interface layouts (interface IDs and method order) were cross-checked against these MIT-licensed projects, then verified independently on the target machine with `spikes/VdProbe`:

- **VirtualDesktop** by Markus Scholtes — https://github.com/MScholtes/VirtualDesktop (MIT)
- **VirtualDesktopAccessor** by Jari Pennanen (Ciantic) — https://github.com/Ciantic/VirtualDesktopAccessor (MIT)

Thank you to both authors for documenting these interfaces across Windows builds.

## Test-only components (not shipped)

xUnit (Apache-2.0), Microsoft.NET.Test.Sdk (MIT), Microsoft.Extensions.TimeProvider.Testing (MIT), coverlet (MIT).
