#import <Metal/Metal.h>
#import <Foundation/Foundation.h>
#include "../include/DCMetalInterop.h"
#include <vector>
#include <map>

// Global state management
static std::map<void*, id> g_objectRetainMap;
static dispatch_queue_t g_completionQueue;

static void ensureCompletionQueue() {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        g_completionQueue = dispatch_queue_create("com.dotcompute.metal.completion", DISPATCH_QUEUE_CONCURRENT);
    });
}

extern "C" {

// System Detection
bool DCMetal_IsMetalSupported(void) {
    @autoreleasepool {
        // Check if Metal is available on this system
        id<MTLDevice> device = MTLCreateSystemDefaultDevice();
        return device != nil;
    }
}

// Device Management Implementation
DCMetalDevice DCMetal_CreateSystemDefaultDevice(void) {
    @autoreleasepool {
        id<MTLDevice> device = MTLCreateSystemDefaultDevice();
        if (device) {
            g_objectRetainMap[(__bridge void*)device] = device;
            return (__bridge_retained DCMetalDevice)device;
        }
        return nullptr;
    }
}

DCMetalDevice DCMetal_CreateDeviceAtIndex(int index) {
    @autoreleasepool {
        NSArray<id<MTLDevice>>* devices = MTLCopyAllDevices();
        if (index >= 0 && index < devices.count) {
            id<MTLDevice> device = devices[index];
            g_objectRetainMap[(__bridge void*)device] = device;
            return (__bridge_retained DCMetalDevice)device;
        }
        return nullptr;
    }
}

int DCMetal_GetDeviceCount(void) {
    @autoreleasepool {
        NSArray<id<MTLDevice>>* devices = MTLCopyAllDevices();
        return (int)devices.count;
    }
}

void DCMetal_ReleaseDevice(DCMetalDevice device) {
    if (device) {
        @autoreleasepool {
            g_objectRetainMap.erase(device);
            CFRelease(device);
        }
    }
}

DCMetalDeviceInfo DCMetal_GetDeviceInfo(DCMetalDevice device) {
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;
        DCMetalDeviceInfo info = {};
        
        // Basic device information
        NSString* deviceName = mtlDevice.name;
        static std::string nameStorage;
        nameStorage = [deviceName UTF8String];
        info.name = nameStorage.c_str();
        
        info.registryID = mtlDevice.registryID;
        info.hasUnifiedMemory = mtlDevice.hasUnifiedMemory;
        info.isLowPower = mtlDevice.isLowPower;
        info.isRemovable = mtlDevice.isRemovable;
        
        // Thread group capabilities
        MTLSize maxThreads = mtlDevice.maxThreadsPerThreadgroup;
        info.maxThreadsPerThreadgroup = maxThreads.width * maxThreads.height * maxThreads.depth;
        info.maxThreadgroupSize = info.maxThreadsPerThreadgroup;
        info.maxBufferLength = mtlDevice.maxBufferLength;
        
        // Memory information
        if (@available(macOS 10.12, *)) {
            info.recommendedMaxWorkingSetSize = mtlDevice.recommendedMaxWorkingSetSize;
        } else {
            info.recommendedMaxWorkingSetSize = info.maxBufferLength / 2; // Fallback estimate
        }
        
        // Location information
        if (@available(macOS 10.13, *)) {
            info.location = (DCMetalDeviceLocation)mtlDevice.location;
            info.locationNumber = mtlDevice.locationNumber;
        } else {
            info.location = DCMetalDeviceLocationUnspecified;
            info.locationNumber = 0;
        }
        
        // Build supported families string
        NSMutableArray* families = [NSMutableArray array];
        if (@available(macOS 10.15, *)) {
            // Apple Silicon families
            if ([mtlDevice supportsFamily:MTLGPUFamilyApple1]) [families addObject:@"Apple1"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyApple2]) [families addObject:@"Apple2"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyApple3]) [families addObject:@"Apple3"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyApple4]) [families addObject:@"Apple4"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyApple5]) [families addObject:@"Apple5"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyApple6]) [families addObject:@"Apple6"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyApple7]) [families addObject:@"Apple7"];
            if (@available(macOS 12.0, *)) {
                if ([mtlDevice supportsFamily:MTLGPUFamilyApple8]) [families addObject:@"Apple8"];
            }
            // Intel Mac families
            if ([mtlDevice supportsFamily:MTLGPUFamilyMac1]) [families addObject:@"Mac1"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyMac2]) [families addObject:@"Mac2"];
            // Common families
            if ([mtlDevice supportsFamily:MTLGPUFamilyCommon1]) [families addObject:@"Common1"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyCommon2]) [families addObject:@"Common2"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyCommon3]) [families addObject:@"Common3"];
        } else {
            // Fallback for older systems
            [families addObject:@"Legacy"];
        }
        
        NSString* familiesString = [families componentsJoinedByString:@","];
        static std::string familiesStorage;
        familiesStorage = [familiesString UTF8String];
        info.supportedFamilies = familiesStorage.c_str();
        
        return info;
    }
}

// Command Queue
DCMetalCommandQueue DCMetal_CreateCommandQueue(DCMetalDevice device) {
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;
        id<MTLCommandQueue> queue = [mtlDevice newCommandQueue];
        if (queue) {
            g_objectRetainMap[(__bridge void*)queue] = queue;
            return (__bridge_retained DCMetalCommandQueue)queue;
        }
        return nullptr;
    }
}

void DCMetal_ReleaseCommandQueue(DCMetalCommandQueue queue) {
    if (queue) {
        @autoreleasepool {
            g_objectRetainMap.erase(queue);
            CFRelease(queue);
        }
    }
}

// Command Buffer
DCMetalCommandBuffer DCMetal_CreateCommandBuffer(DCMetalCommandQueue queue) {
    @autoreleasepool {
        id<MTLCommandQueue> mtlQueue = (__bridge id<MTLCommandQueue>)queue;
        id<MTLCommandBuffer> buffer = [mtlQueue commandBuffer];
        if (buffer) {
            g_objectRetainMap[(__bridge void*)buffer] = buffer;
            return (__bridge_retained DCMetalCommandBuffer)buffer;
        }
        return nullptr;
    }
}

void DCMetal_CommitCommandBuffer(DCMetalCommandBuffer buffer) {
    @autoreleasepool {
        id<MTLCommandBuffer> mtlBuffer = (__bridge id<MTLCommandBuffer>)buffer;
        [mtlBuffer commit];
    }
}

void DCMetal_WaitUntilCompleted(DCMetalCommandBuffer buffer) {
    @autoreleasepool {
        id<MTLCommandBuffer> mtlBuffer = (__bridge id<MTLCommandBuffer>)buffer;
        [mtlBuffer waitUntilCompleted];
    }
}

void DCMetal_ReleaseCommandBuffer(DCMetalCommandBuffer buffer) {
    if (buffer) {
        @autoreleasepool {
            g_objectRetainMap.erase(buffer);
            CFRelease(buffer);
        }
    }
}

void DCMetal_SetCommandBufferCompletionHandler(DCMetalCommandBuffer buffer, DCMetalCommandBufferCompletionHandler handler) {
    @autoreleasepool {
        ensureCompletionQueue();
        id<MTLCommandBuffer> mtlBuffer = (__bridge id<MTLCommandBuffer>)buffer;
        
        [mtlBuffer addCompletedHandler:^(id<MTLCommandBuffer> completedBuffer) {
            DCMetalCommandBufferStatus status;
            switch (completedBuffer.status) {
                case MTLCommandBufferStatusNotEnqueued:
                    status = DCMetalCommandBufferStatusNotEnqueued;
                    break;
                case MTLCommandBufferStatusEnqueued:
                    status = DCMetalCommandBufferStatusEnqueued;
                    break;
                case MTLCommandBufferStatusCommitted:
                    status = DCMetalCommandBufferStatusCommitted;
                    break;
                case MTLCommandBufferStatusScheduled:
                    status = DCMetalCommandBufferStatusScheduled;
                    break;
                case MTLCommandBufferStatusCompleted:
                    status = DCMetalCommandBufferStatusCompleted;
                    break;
                case MTLCommandBufferStatusError:
                    status = DCMetalCommandBufferStatusError;
                    break;
                default:
                    status = DCMetalCommandBufferStatusError;
                    break;
            }
            
            dispatch_async(g_completionQueue, ^{
                handler(status);
            });
        }];
    }
}

// Buffers
DCMetalBuffer DCMetal_CreateBuffer(DCMetalDevice device, size_t length, DCMetalStorageMode mode) {
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;
        MTLResourceOptions options;
        
        switch (mode) {
            case DCMetalStorageModeShared:
                options = MTLResourceStorageModeShared;
                break;
            case DCMetalStorageModePrivate:
                options = MTLResourceStorageModePrivate;
                break;
            case DCMetalStorageModeManaged:
                options = MTLResourceStorageModeManaged;
                break;
            case DCMetalStorageModeMemoryless:
                if (@available(macOS 11.0, *)) {
                    options = MTLResourceStorageModeMemoryless;
                } else {
                    options = MTLResourceStorageModePrivate; // Fallback
                }
                break;
            default:
                options = MTLResourceStorageModeShared;
                break;
        }
        
        id<MTLBuffer> buffer = [mtlDevice newBufferWithLength:length options:options];
        if (buffer) {
            g_objectRetainMap[(__bridge void*)buffer] = buffer;
            return (__bridge_retained DCMetalBuffer)buffer;
        }
        return nullptr;
    }
}

DCMetalBuffer DCMetal_CreateBufferWithBytes(DCMetalDevice device, const void* bytes, size_t length, DCMetalStorageMode mode) {
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;
        MTLResourceOptions options;
        
        switch (mode) {
            case DCMetalStorageModeShared:
                options = MTLResourceStorageModeShared;
                break;
            case DCMetalStorageModePrivate:
                options = MTLResourceStorageModePrivate;
                break;
            case DCMetalStorageModeManaged:
                options = MTLResourceStorageModeManaged;
                break;
            case DCMetalStorageModeMemoryless:
                if (@available(macOS 11.0, *)) {
                    options = MTLResourceStorageModeMemoryless;
                } else {
                    options = MTLResourceStorageModePrivate; // Fallback
                }
                break;
            default:
                options = MTLResourceStorageModeShared;
                break;
        }
        
        id<MTLBuffer> buffer = [mtlDevice newBufferWithBytes:bytes length:length options:options];
        if (buffer) {
            g_objectRetainMap[(__bridge void*)buffer] = buffer;
            return (__bridge_retained DCMetalBuffer)buffer;
        }
        return nullptr;
    }
}

void* DCMetal_GetBufferContents(DCMetalBuffer buffer) {
    @autoreleasepool {
        id<MTLBuffer> mtlBuffer = (__bridge id<MTLBuffer>)buffer;
        return mtlBuffer.contents;
    }
}

size_t DCMetal_GetBufferLength(DCMetalBuffer buffer) {
    @autoreleasepool {
        id<MTLBuffer> mtlBuffer = (__bridge id<MTLBuffer>)buffer;
        return mtlBuffer.length;
    }
}

void DCMetal_ReleaseBuffer(DCMetalBuffer buffer) {
    if (buffer) {
        @autoreleasepool {
            g_objectRetainMap.erase(buffer);
            CFRelease(buffer);
        }
    }
}

void DCMetal_DidModifyRange(DCMetalBuffer buffer, int64_t offset, int64_t length) {
    @autoreleasepool {
        id<MTLBuffer> mtlBuffer = (__bridge id<MTLBuffer>)buffer;
        if (mtlBuffer.storageMode == MTLStorageModeManaged) {
            [mtlBuffer didModifyRange:NSMakeRange(offset, length)];
        }
    }
}

void DCMetal_CopyBuffer(DCMetalBuffer source, int64_t sourceOffset, DCMetalBuffer destination, int64_t destOffset, int64_t size) {
    @autoreleasepool {
        id<MTLBuffer> srcBuffer = (__bridge id<MTLBuffer>)source;
        id<MTLBuffer> dstBuffer = (__bridge id<MTLBuffer>)destination;
        
        // Use memcpy for direct buffer copy - this is synchronous but simple
        void* srcPtr = (char*)srcBuffer.contents + sourceOffset;
        void* dstPtr = (char*)dstBuffer.contents + destOffset;
        memcpy(dstPtr, srcPtr, size);
        
        // Mark modified ranges if needed
        if (dstBuffer.storageMode == MTLStorageModeManaged) {
            [dstBuffer didModifyRange:NSMakeRange(destOffset, size)];
        }
    }
}

// Compilation
DCMetalCompileOptions DCMetal_CreateCompileOptions(void) {
    @autoreleasepool {
        MTLCompileOptions* options = [[MTLCompileOptions alloc] init];
        if (options) {
            g_objectRetainMap[(__bridge void*)options] = options;
            return (__bridge_retained DCMetalCompileOptions)options;
        }
        return nullptr;
    }
}

void DCMetal_SetCompileOptionsFastMath(DCMetalCompileOptions options, bool enable) {
    @autoreleasepool {
        MTLCompileOptions* mtlOptions = (__bridge MTLCompileOptions*)options;
        mtlOptions.fastMathEnabled = enable;
    }
}

void DCMetal_SetCompileOptionsLanguageVersion(DCMetalCompileOptions options, DCMetalLanguageVersion version) {
    @autoreleasepool {
        MTLCompileOptions* mtlOptions = (__bridge MTLCompileOptions*)options;
        
        MTLLanguageVersion langVersion;
        switch (version) {
            case DCMetalLanguageVersion10:
                langVersion = MTLLanguageVersion1_0;
                break;
            case DCMetalLanguageVersion11:
                langVersion = MTLLanguageVersion1_1;
                break;
            case DCMetalLanguageVersion12:
                langVersion = MTLLanguageVersion1_2;
                break;
            case DCMetalLanguageVersion20:
                langVersion = MTLLanguageVersion2_0;
                break;
            case DCMetalLanguageVersion21:
                langVersion = MTLLanguageVersion2_1;
                break;
            case DCMetalLanguageVersion22:
                langVersion = MTLLanguageVersion2_2;
                break;
            case DCMetalLanguageVersion23:
                langVersion = MTLLanguageVersion2_3;
                break;
            case DCMetalLanguageVersion24:
                langVersion = MTLLanguageVersion2_4;
                break;
            case DCMetalLanguageVersion30:
                if (@available(macOS 12.0, *)) {
                    langVersion = MTLLanguageVersion3_0;
                } else {
                    langVersion = MTLLanguageVersion2_4; // Fallback
                }
                break;
            case DCMetalLanguageVersion31:
                if (@available(macOS 13.0, *)) {
                    langVersion = MTLLanguageVersion3_1;
                } else {
                    langVersion = MTLLanguageVersion3_0; // Fallback
                }
                break;
            default:
                langVersion = MTLLanguageVersion2_4;
                break;
        }
        
        mtlOptions.languageVersion = langVersion;
    }
}

void DCMetal_ReleaseCompileOptions(DCMetalCompileOptions options) {
    if (options) {
        @autoreleasepool {
            g_objectRetainMap.erase(options);
            CFRelease(options);
        }
    }
}

DCMetalLibrary DCMetal_CompileLibrary(DCMetalDevice device, const char* source, DCMetalCompileOptions options, DCMetalError* error) {
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;
        MTLCompileOptions* mtlOptions = (__bridge MTLCompileOptions*)options;
        NSString* sourceString = [NSString stringWithUTF8String:source];
        
        NSError* nsError = nil;
        id<MTLLibrary> library = [mtlDevice newLibraryWithSource:sourceString
                                                         options:mtlOptions
                                                           error:&nsError];
        
        if (!library) {
            if (error && nsError) {
                g_objectRetainMap[(__bridge void*)nsError] = nsError;
                *error = (__bridge_retained DCMetalError)nsError;
            }
            return nullptr;
        }
        
        g_objectRetainMap[(__bridge void*)library] = library;
        return (__bridge_retained DCMetalLibrary)library;
    }
}

DCMetalLibrary DCMetal_CreateLibraryWithSource(DCMetalDevice device, const char* source) {
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;
        NSString* sourceString = [NSString stringWithUTF8String:source];
        
        NSError* error = nil;
        id<MTLLibrary> library = [mtlDevice newLibraryWithSource:sourceString
                                                         options:nil
                                                           error:&error];
        
        if (library) {
            g_objectRetainMap[(__bridge void*)library] = library;
            return (__bridge_retained DCMetalLibrary)library;
        }
        return nullptr;
    }
}

void DCMetal_ReleaseLibrary(DCMetalLibrary library) {
    if (library) {
        @autoreleasepool {
            g_objectRetainMap.erase(library);
            CFRelease(library);
        }
    }
}

DCMetalFunction DCMetal_GetFunction(DCMetalLibrary library, const char* name) {
    @autoreleasepool {
        id<MTLLibrary> mtlLibrary = (__bridge id<MTLLibrary>)library;
        NSString* functionName = [NSString stringWithUTF8String:name];
        
        id<MTLFunction> function = [mtlLibrary newFunctionWithName:functionName];
        if (function) {
            g_objectRetainMap[(__bridge void*)function] = function;
            return (__bridge_retained DCMetalFunction)function;
        }
        return nullptr;
    }
}

void DCMetal_ReleaseFunction(DCMetalFunction function) {
    if (function) {
        @autoreleasepool {
            g_objectRetainMap.erase(function);
            CFRelease(function);
        }
    }
}

// Pipeline State
DCMetalComputePipelineState DCMetal_CreateComputePipelineState(DCMetalDevice device, DCMetalFunction function, DCMetalError* error) {
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;
        id<MTLFunction> mtlFunction = (__bridge id<MTLFunction>)function;
        
        NSError* nsError = nil;
        id<MTLComputePipelineState> pipelineState = [mtlDevice newComputePipelineStateWithFunction:mtlFunction
                                                                                              error:&nsError];
        
        if (!pipelineState) {
            if (error && nsError) {
                g_objectRetainMap[(__bridge void*)nsError] = nsError;
                *error = (__bridge_retained DCMetalError)nsError;
            }
            return nullptr;
        }
        
        g_objectRetainMap[(__bridge void*)pipelineState] = pipelineState;
        return (__bridge_retained DCMetalComputePipelineState)pipelineState;
    }
}

void DCMetal_ReleasePipelineState(DCMetalComputePipelineState state) {
    if (state) {
        @autoreleasepool {
            g_objectRetainMap.erase(state);
            CFRelease(state);
        }
    }
}

void DCMetal_ReleaseComputePipelineState(DCMetalComputePipelineState state) {
    DCMetal_ReleasePipelineState(state);
}

int DCMetal_GetMaxTotalThreadsPerThreadgroup(DCMetalComputePipelineState state) {
    @autoreleasepool {
        id<MTLComputePipelineState> pipelineState = (__bridge id<MTLComputePipelineState>)state;
        return (int)pipelineState.maxTotalThreadsPerThreadgroup;
    }
}

void DCMetal_GetThreadExecutionWidth(DCMetalComputePipelineState state, int* x, int* y, int* z) {
    @autoreleasepool {
        id<MTLComputePipelineState> pipelineState = (__bridge id<MTLComputePipelineState>)state;
        NSUInteger width = pipelineState.threadExecutionWidth;
        
        // Metal threadExecutionWidth is typically a 1D value
        // For compatibility, we return it as x component
        *x = (int)width;
        *y = 1;
        *z = 1;
    }
}

// Compute Command Encoder
DCMetalCommandEncoder DCMetal_CreateComputeCommandEncoder(DCMetalCommandBuffer buffer) {
    @autoreleasepool {
        id<MTLCommandBuffer> mtlBuffer = (__bridge id<MTLCommandBuffer>)buffer;
        id<MTLComputeCommandEncoder> encoder = [mtlBuffer computeCommandEncoder];
        
        if (encoder) {
            g_objectRetainMap[(__bridge void*)encoder] = encoder;
            return (__bridge_retained DCMetalCommandEncoder)encoder;
        }
        return nullptr;
    }
}

void DCMetal_SetComputePipelineState(DCMetalCommandEncoder encoder, DCMetalComputePipelineState state) {
    @autoreleasepool {
        id<MTLComputeCommandEncoder> mtlEncoder = (__bridge id<MTLComputeCommandEncoder>)encoder;
        id<MTLComputePipelineState> pipelineState = (__bridge id<MTLComputePipelineState>)state;
        
        [mtlEncoder setComputePipelineState:pipelineState];
    }
}

void DCMetal_SetBuffer(DCMetalCommandEncoder encoder, DCMetalBuffer buffer, size_t offset, int index) {
    @autoreleasepool {
        id<MTLComputeCommandEncoder> mtlEncoder = (__bridge id<MTLComputeCommandEncoder>)encoder;
        id<MTLBuffer> mtlBuffer = (__bridge id<MTLBuffer>)buffer;
        
        [mtlEncoder setBuffer:mtlBuffer offset:offset atIndex:index];
    }
}

void DCMetal_SetBytes(DCMetalCommandEncoder encoder, const void* bytes, size_t length, int index) {
    @autoreleasepool {
        id<MTLComputeCommandEncoder> mtlEncoder = (__bridge id<MTLComputeCommandEncoder>)encoder;
        
        [mtlEncoder setBytes:bytes length:length atIndex:index];
    }
}

void DCMetal_DispatchThreadgroups(DCMetalCommandEncoder encoder, DCMetalSize gridSize, DCMetalSize threadgroupSize) {
    @autoreleasepool {
        id<MTLComputeCommandEncoder> mtlEncoder = (__bridge id<MTLComputeCommandEncoder>)encoder;
        
        MTLSize grid = MTLSizeMake(gridSize.width, gridSize.height, gridSize.depth);
        MTLSize threadgroup = MTLSizeMake(threadgroupSize.width, threadgroupSize.height, threadgroupSize.depth);
        
        [mtlEncoder dispatchThreadgroups:grid threadsPerThreadgroup:threadgroup];
    }
}

void DCMetal_EndEncoding(DCMetalCommandEncoder encoder) {
    @autoreleasepool {
        id<MTLComputeCommandEncoder> mtlEncoder = (__bridge id<MTLComputeCommandEncoder>)encoder;
        [mtlEncoder endEncoding];
    }
}

void DCMetal_ReleaseEncoder(DCMetalCommandEncoder encoder) {
    if (encoder) {
        @autoreleasepool {
            g_objectRetainMap.erase(encoder);
            CFRelease(encoder);
        }
    }
}

// Error Handling
const char* DCMetal_GetErrorLocalizedDescription(DCMetalError error) {
    @autoreleasepool {
        NSError* nsError = (__bridge NSError*)error;
        NSString* description = nsError.localizedDescription;
        
        static std::string descriptionStorage;
        descriptionStorage = [description UTF8String];
        return descriptionStorage.c_str();
    }
}

void DCMetal_ReleaseError(DCMetalError error) {
    if (error) {
        @autoreleasepool {
            g_objectRetainMap.erase(error);
            CFRelease(error);
        }
    }
}

} // extern "C"