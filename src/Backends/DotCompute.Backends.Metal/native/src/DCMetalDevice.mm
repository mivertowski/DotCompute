#import <Metal/Metal.h>
#import <Foundation/Foundation.h>
#include "../include/DCMetalInterop.h"

// Device Management Implementation
extern "C" {

DCMetalDevice DCMetal_CreateSystemDefaultDevice(void) {
    @autoreleasepool {
        id<MTLDevice> device = MTLCreateSystemDefaultDevice();
        return (__bridge_retained DCMetalDevice)device;
    }
}

DCMetalDevice DCMetal_CreateDeviceAtIndex(int index) {
    @autoreleasepool {
        NSArray<id<MTLDevice>>* devices = MTLCopyAllDevices();
        if (index >= 0 && index < devices.count) {
            id<MTLDevice> device = devices[index];
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
        CFRelease(device);
    }
}

DCMetalDeviceInfo DCMetal_GetDeviceInfo(DCMetalDevice device) {
    @autoreleasepool {
        id<MTLDevice> mtlDevice = (__bridge id<MTLDevice>)device;
        DCMetalDeviceInfo info = {};
        
        info.name = [mtlDevice.name UTF8String];
        info.registryID = mtlDevice.registryID;
        info.hasUnifiedMemory = mtlDevice.hasUnifiedMemory;
        info.isLowPower = mtlDevice.isLowPower;
        info.isRemovable = mtlDevice.isRemovable;
        info.maxThreadgroupSize = mtlDevice.maxThreadsPerThreadgroup.width * 
                                  mtlDevice.maxThreadsPerThreadgroup.height * 
                                  mtlDevice.maxThreadsPerThreadgroup.depth;
        info.maxBufferLength = mtlDevice.maxBufferLength;
        
        if (@available(macOS 10.12, *)) {
            info.recommendedMaxWorkingSetSize = mtlDevice.recommendedMaxWorkingSetSize;
        }
        
        // Get location info
        if (@available(macOS 10.13, *)) {
            info.location = (DCMetalDeviceLocation)mtlDevice.location;
            info.locationNumber = mtlDevice.locationNumber;
        }
        
        // Build supported families string
        NSMutableArray* families = [NSMutableArray array];
        if (@available(macOS 10.15, *)) {
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
            if ([mtlDevice supportsFamily:MTLGPUFamilyMac1]) [families addObject:@"Mac1"];
            if ([mtlDevice supportsFamily:MTLGPUFamilyMac2]) [families addObject:@"Mac2"];
        }
        
        info.supportedFamilies = [[families componentsJoinedByString:@","] UTF8String];
        
        return info;
    }
}

} // extern "C"