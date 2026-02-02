# DOTCOMPUTE0001: Mobile Extensions (MAUI) - Experimental

## Summary

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DOTCOMPUTE0001 |
| **Severity** | Warning |
| **Category** | Experimental |
| **Affected APIs** | `DotCompute.Mobile.MAUI.MauiComputeService` |

## Description

The MAUI mobile compute service is marked as experimental because the backend implementations for iOS, Android, and Windows mobile platforms are currently placeholders. Using this API in production may result in:

- **iOS/macOS**: Metal backend returns stub implementations
- **Android**: Vulkan backend returns stub implementations
- **Windows Mobile**: DirectML backend returns stub implementations

All buffer operations use a `PlaceholderBuffer<T>` that stores data in managed memory without GPU acceleration.

## Current Status

| Platform | Backend | Status |
|----------|---------|--------|
| iOS | Metal | Placeholder (no GPU acceleration) |
| macOS | Metal | Partial (can use main Metal backend) |
| Android | Vulkan | Placeholder (no GPU acceleration) |
| Windows | DirectML | Placeholder (no GPU acceleration) |

## When to Suppress

You may suppress this warning if:

1. You are developing and testing the mobile UI without requiring GPU compute
2. You understand the performance implications of CPU-only execution
3. You are contributing to the mobile backend implementation

## How to Suppress

### In Code

```csharp
#pragma warning disable DOTCOMPUTE0001
var service = new MauiComputeService();
#pragma warning restore DOTCOMPUTE0001
```

### In Project File

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);DOTCOMPUTE0001</NoWarn>
</PropertyGroup>
```

### Using SuppressMessage Attribute

```csharp
[SuppressMessage("Experimental", "DOTCOMPUTE0001:Mobile extensions are experimental")]
public void MyMethod()
{
    var service = new MauiComputeService();
}
```

## Workarounds

For production mobile applications requiring GPU compute:

1. **iOS/macOS**: Use the main `DotCompute.Backends.Metal` package directly
2. **Android**: Consider using NNAPI or TensorFlow Lite for GPU acceleration
3. **Windows**: Use the main `DotCompute.Backends.CUDA` or DirectML packages

## Roadmap

Mobile GPU compute support is planned for future releases:

- **v0.7.0**: iOS Metal backend integration
- **v0.8.0**: Android Vulkan backend integration
- **v1.0.0**: Full mobile compute parity

## Related Resources

- [Mobile Development Guide](../guides/mobile-development.md)
- [Metal Backend Documentation](../metal/index.md)
- [Platform Support Matrix](../articles/platform-support.md)
