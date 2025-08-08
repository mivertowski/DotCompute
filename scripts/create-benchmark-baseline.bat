@echo off
REM DotCompute Benchmark Baseline Creation Script for Windows
REM This script creates performance baselines for all benchmark suites

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set datetime=%%I
set TIMESTAMP=%datetime:~0,8%_%datetime:~8,6%
set BASELINE_DIR=%PROJECT_ROOT%\benchmark-baselines\%TIMESTAMP%

echo ================================================
echo   DotCompute Performance Benchmark Baseline
echo ================================================
echo.
echo Timestamp: %TIMESTAMP%
echo Output Directory: %BASELINE_DIR%
echo.

REM Create baseline directory
if not exist "%BASELINE_DIR%" mkdir "%BASELINE_DIR%"

REM Navigate to project root
cd /d "%PROJECT_ROOT%"

REM Restore and build all projects
echo Restoring dependencies...
dotnet restore
if errorlevel 1 goto error

echo Building projects...
dotnet build --configuration Release --no-restore
if errorlevel 1 goto error

echo.
echo Running Benchmark Suites
echo ============================
echo.

REM Memory benchmarks
call :run_benchmark "Memory" "tests\DotCompute.Memory.Tests\DotCompute.Memory.Tests.csproj" "Category=Benchmark"

REM Core benchmarks
call :run_benchmark "Core" "tests\DotCompute.Core.Tests\DotCompute.Core.Tests.csproj" "Category=Benchmark"

REM Abstractions benchmarks
call :run_benchmark "Abstractions" "tests\DotCompute.Abstractions.Tests\DotCompute.Abstractions.Tests.csproj" "Category=Benchmark"

REM Plugins benchmarks
call :run_benchmark "Plugins" "tests\DotCompute.Plugins.Tests\DotCompute.Plugins.Tests.csproj" "Category=Benchmark"

REM Integration benchmarks
call :run_benchmark "Integration" "tests\DotCompute.Integration.Tests\DotCompute.Integration.Tests.csproj" "Category=Benchmark^|Category=Performance"

REM Generate summary report
echo.
echo Generating Summary Report
echo ============================

REM Get git information
for /f "delims=" %%i in ('git branch --show-current') do set GIT_BRANCH=%%i
for /f "delims=" %%i in ('git rev-parse HEAD') do set GIT_COMMIT=%%i
for /f "delims=" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i

REM Create summary file
(
echo # DotCompute Benchmark Baseline
echo.
echo ## Metadata
echo - **Date**: %date% %time%
echo - **Timestamp**: %TIMESTAMP%
echo - **Git Branch**: %GIT_BRANCH%
echo - **Git Commit**: %GIT_COMMIT%
echo.
echo ## Environment
echo - **OS**: Windows
echo - **Computer Name**: %COMPUTERNAME%
echo - **.NET Version**: %DOTNET_VERSION%
echo.
echo ## Benchmark Results
echo.
echo ### Test Suites Executed
) > "%BASELINE_DIR%\summary.md"

REM List executed benchmarks
for /d %%D in ("%BASELINE_DIR%\*") do (
    if exist "%%D\results.trx" (
        echo - OK %%~nD >> "%BASELINE_DIR%\summary.md"
    ) else (
        echo - FAILED %%~nD >> "%BASELINE_DIR%\summary.md"
    )
)

echo. >> "%BASELINE_DIR%\summary.md"
echo ## File Locations >> "%BASELINE_DIR%\summary.md"
echo. >> "%BASELINE_DIR%\summary.md"
echo Benchmark results are stored in: >> "%BASELINE_DIR%\summary.md"
echo ``` >> "%BASELINE_DIR%\summary.md"
echo %BASELINE_DIR% >> "%BASELINE_DIR%\summary.md"
echo ``` >> "%BASELINE_DIR%\summary.md"

echo.
echo Benchmark baseline created successfully!
echo.
echo Results saved to:
echo   %BASELINE_DIR%
echo.
echo Summary report:
echo   %BASELINE_DIR%\summary.md
echo.

REM Check if any .trx files were created
dir /s /b "%BASELINE_DIR%\*.trx" >nul 2>&1
if errorlevel 1 (
    echo Warning: No benchmark results were generated
    echo This might indicate that no benchmarks are defined with [Benchmark] attribute
    exit /b 1
) else (
    echo Benchmark data collected successfully
    exit /b 0
)

:run_benchmark
set PROJECT_NAME=%~1
set PROJECT_PATH=%~2
set FILTER=%~3

echo ----------------------------------------
echo Running benchmarks for: %PROJECT_NAME%
echo Filter: %FILTER%
echo ----------------------------------------

if exist "%PROJECT_PATH%" (
    REM Create project-specific output directory
    set OUTPUT_DIR=%BASELINE_DIR%\%PROJECT_NAME%
    if not exist "!OUTPUT_DIR!" mkdir "!OUTPUT_DIR!"
    
    REM Run the benchmark
    dotnet test "%PROJECT_PATH%" ^
        --configuration Release ^
        --filter "%FILTER%" ^
        --logger "trx;LogFileName=!OUTPUT_DIR!\results.trx" ^
        --results-directory "!OUTPUT_DIR!" ^
        -- RunConfiguration.TestSessionTimeout=1800000
    
    if errorlevel 1 (
        echo Warning: Benchmark failed for %PROJECT_NAME%
    ) else (
        echo Completed: %PROJECT_NAME%
    )
    echo.
) else (
    echo Warning: Project not found: %PROJECT_PATH%
    echo.
)
goto :eof

:error
echo.
echo Error occurred during benchmark execution
exit /b 1