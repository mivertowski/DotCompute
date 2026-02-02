# DOTCOMPUTE0002: Web Extensions (Blazor WebAssembly) - Experimental

## Summary

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DOTCOMPUTE0002 |
| **Severity** | Warning |
| **Category** | Experimental |
| **Affected APIs** | `DotCompute.Web.Blazor.BlazorComputeService` |

## Description

The Blazor WebAssembly compute service is marked as experimental because WebGPU support in browsers is still evolving and the JavaScript interop layer is incomplete. Using this API in production may result in:

- **WebGPU**: Not yet implemented (returns `false` for availability)
- **WebGL2**: Placeholder implementation with limited compute capabilities
- **JS Interop**: Several methods contain placeholder implementations

## Current Status

| Feature | Status |
|---------|--------|
| WebGPU Detection | Placeholder (always returns `false`) |
| WebGL2 Detection | Placeholder (always returns `true`) |
| Buffer Allocation | Basic implementation |
| Kernel Execution | Not implemented |
| Memory Transfer | Limited |

## Browser Support Matrix

| Browser | WebGPU | WebGL2 |
|---------|--------|--------|
| Chrome 113+ | Partial | Yes |
| Firefox | Experimental | Yes |
| Safari 17+ | Partial | Yes |
| Edge 113+ | Partial | Yes |

## When to Suppress

You may suppress this warning if:

1. You are prototyping web-based compute applications
2. You only need basic WebGL2 rendering (not compute)
3. You are targeting browsers with WebGPU support and accept the risk
4. You are contributing to the web backend implementation

## How to Suppress

### In Code

```csharp
#pragma warning disable DOTCOMPUTE0002
var service = new BlazorComputeService(jsRuntime);
#pragma warning restore DOTCOMPUTE0002
```

### In Project File

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);DOTCOMPUTE0002</NoWarn>
</PropertyGroup>
```

### Using SuppressMessage Attribute

```csharp
[SuppressMessage("Experimental", "DOTCOMPUTE0002:Web extensions are experimental")]
public async Task InitializeCompute()
{
    var service = new BlazorComputeService(jsRuntime);
}
```

## Workarounds

For production web applications requiring GPU compute:

1. **Server-Side Blazor**: Use server-side rendering with full GPU access
2. **Web Workers + WASM**: Consider using compute.js or gpu.js libraries
3. **Hybrid Approach**: Offload heavy compute to a backend API

## Known Limitations

1. **Memory Constraints**: WebAssembly has a 4GB memory limit
2. **No Shared Memory**: WebGPU buffers cannot be directly shared with WASM
3. **Async Required**: All GPU operations must be async due to browser event loop
4. **Security Sandbox**: Limited access to system GPU features

## Roadmap

Web GPU compute support is planned for future releases:

- **v0.7.0**: WebGPU basic support (Chrome/Edge)
- **v0.8.0**: Cross-browser WebGPU support
- **v1.0.0**: Full WebGPU compute parity

## Related Resources

- [WebGPU Specification](https://www.w3.org/TR/webgpu/)
- [Blazor WebAssembly Documentation](https://docs.microsoft.com/aspnet/core/blazor/)
- [Browser Compatibility](../articles/browser-compatibility.md)
