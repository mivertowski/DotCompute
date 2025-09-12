cmake_minimum_required(VERSION 3.20)
project(DotComputeMetal LANGUAGES CXX OBJCXX VERSION 1.0.0)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_OBJCXX_STANDARD 17)
set(CMAKE_OBJCXX_STANDARD_REQUIRED ON)

# Set deployment target for Metal support
if(CMAKE_OSX_DEPLOYMENT_TARGET VERSION_LESS "10.13")
    set(CMAKE_OSX_DEPLOYMENT_TARGET "10.13")
    message(STATUS "Setting minimum macOS deployment target to 10.13 for Metal support")
endif()

# Find required frameworks
find_library(METAL_FRAMEWORK Metal REQUIRED)
find_library(FOUNDATION_FRAMEWORK Foundation REQUIRED)

# MetalKit and MPS are optional - they may not be available on all systems
find_library(METALKIT_FRAMEWORK MetalKit)
find_library(METALPERFORMANCESHADERS_FRAMEWORK MetalPerformanceShaders)

if(NOT METAL_FRAMEWORK)
    message(FATAL_ERROR "Metal framework not found - ensure you're running on macOS 10.11 or later")
endif()

# Source files - only include the main implementation
set(SOURCES
    src/DCMetalDevice.mm
)

# Create shared library
add_library(DotComputeMetal SHARED ${SOURCES})

# Include directories
target_include_directories(DotComputeMetal PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# Link frameworks
target_link_libraries(DotComputeMetal PRIVATE
    ${METAL_FRAMEWORK}
    ${FOUNDATION_FRAMEWORK}
)

# Link optional frameworks if available
if(METALKIT_FRAMEWORK)
    target_link_libraries(DotComputeMetal PRIVATE ${METALKIT_FRAMEWORK})
    target_compile_definitions(DotComputeMetal PRIVATE HAVE_METALKIT=1)
endif()

if(METALPERFORMANCESHADERS_FRAMEWORK)
    target_link_libraries(DotComputeMetal PRIVATE ${METALPERFORMANCESHADERS_FRAMEWORK})
    target_compile_definitions(DotComputeMetal PRIVATE HAVE_MPS=1)
endif()

# Compiler flags for Objective-C++
target_compile_options(DotComputeMetal PRIVATE
    -fobjc-arc                  # Enable ARC
    -Wall                       # Enable warnings
    -Wextra                     # Extra warnings
    -Wno-unused-parameter       # Disable unused parameter warnings
    -fPIC                       # Position independent code
    -fvisibility=default        # Export symbols by default for C functions
    $<$<CONFIG:Release>:-O3>    # Optimize for release
    $<$<CONFIG:Debug>:-g>       # Debug symbols for debug builds
)

# Set target properties
set_target_properties(DotComputeMetal PROPERTIES
    OUTPUT_NAME "DotComputeMetal"
    SUFFIX ".dylib"
    VERSION ${PROJECT_VERSION}
    SOVERSION ${PROJECT_VERSION_MAJOR}
    CXX_VISIBILITY_PRESET default
    OBJCXX_VISIBILITY_PRESET default
    C_VISIBILITY_PRESET default
    VISIBILITY_INLINES_HIDDEN OFF
    MACOSX_RPATH ON
    INSTALL_RPATH "@loader_path;@rpath"
    BUILD_WITH_INSTALL_RPATH TRUE
)

# Installation
install(TARGETS DotComputeMetal
    LIBRARY DESTINATION lib
    RUNTIME DESTINATION bin
)

# Copy to output directory for development
add_custom_command(TARGET DotComputeMetal POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy
    $<TARGET_FILE:DotComputeMetal>
    ${CMAKE_CURRENT_SOURCE_DIR}/../libDotComputeMetal.dylib
    COMMENT "Copying libDotComputeMetal.dylib to parent directory"
)

# Print build information
message(STATUS "Building DotCompute Metal Backend:")
message(STATUS "  Version: ${PROJECT_VERSION}")
message(STATUS "  Deployment Target: ${CMAKE_OSX_DEPLOYMENT_TARGET}")
message(STATUS "  Metal Framework: ${METAL_FRAMEWORK}")
message(STATUS "  Foundation Framework: ${FOUNDATION_FRAMEWORK}")
if(METALKIT_FRAMEWORK)
    message(STATUS "  MetalKit Framework: ${METALKIT_FRAMEWORK}")
endif()
if(METALPERFORMANCESHADERS_FRAMEWORK)
    message(STATUS "  MPS Framework: ${METALPERFORMANCESHADERS_FRAMEWORK}")
endif()