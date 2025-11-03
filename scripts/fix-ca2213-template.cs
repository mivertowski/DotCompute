// Template for adding IDisposable to test classes
//
// Pattern 1: Class without IDisposable
// BEFORE:
// public sealed class MyTests
// {
//     private readonly IAccelerator _accelerator;
// }
//
// AFTER:
// public sealed class MyTests : IDisposable
// {
//     private readonly IAccelerator _accelerator;
//
//     public void Dispose()
//     {
//         _accelerator?.Dispose();
//         GC.SuppressFinalize(this);
//     }
// }
//
// Pattern 2: Class that already inherits from a base
// BEFORE:
// public sealed class MyTests : TestBase
// {
//     private readonly IAccelerator _accelerator;
// }
//
// AFTER:
// public sealed class MyTests : TestBase
// {
//     private readonly IAccelerator _accelerator;
//
//     protected override void Dispose(bool disposing)
//     {
//         if (disposing)
//         {
//             _accelerator?.Dispose();
//         }
//         base.Dispose(disposing);
//     }
// }
//
// Common disposable field patterns to look for:
// - _accelerator (IAccelerator)
// - _context (various context types)
// - _stream (Stream types)
// - _buffer (IBuffer, UnifiedBuffer<T>)
// - _kernel (IKernel, ICompiledKernel)
// - _device (device handles)
// - _compiler (IKernelCompiler)
// - _manager (various manager types)
