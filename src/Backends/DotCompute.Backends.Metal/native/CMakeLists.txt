cmake_minimum_required(VERSION 3.20)
project(DotComputeMetal LANGUAGES CXX OBJCXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_OSX_DEPLOYMENT_TARGET "10.14")

# Find required frameworks
find_library(METAL_FRAMEWORK Metal REQUIRED)
find_library(METALKIT_FRAMEWORK MetalKit REQUIRED)
find_library(METALPERFORMANCESHADERS_FRAMEWORK MetalPerformanceShaders REQUIRED)
find_library(FOUNDATION_FRAMEWORK Foundation REQUIRED)

# Source files
set(SOURCES
    src/DCMetalDevice.mm
    src/DCMetalBuffer.mm
    src/DCMetalCompiler.mm
    src/DCMetalCommandQueue.mm
    src/DCMetalKernel.mm
    src/DCMetalInterop.cpp
)

# Create shared library
add_library(DotComputeMetal SHARED ${SOURCES})

# Include directories
target_include_directories(DotComputeMetal PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# Link frameworks
target_link_libraries(DotComputeMetal
    ${METAL_FRAMEWORK}
    ${METALKIT_FRAMEWORK}
    ${METALPERFORMANCESHADERS_FRAMEWORK}
    ${FOUNDATION_FRAMEWORK}
)

# Compiler flags
target_compile_options(DotComputeMetal PRIVATE
    -Wall
    -Wextra
    -fPIC
    -O3
    -fvisibility=hidden
)

# Export only C API symbols
set_target_properties(DotComputeMetal PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    OBJCXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
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
)