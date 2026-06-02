@echo off
setlocal

:: Find Roslyn compiler
set CSC=
if exist "C:\Program Files\dotnet\sdk\10.0.202\Roslyn\bincore\csc.exe" (
    set "CSC=C:\Program Files\dotnet\sdk\10.0.202\Roslyn\bincore\csc.exe"
) else if exist "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe" (
    set "CSC=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
) else (
    for /f "delims=" %%i in ('dir /b /s "C:\Program Files\Microsoft Visual Studio\*\MSBuild\Current\Bin\Roslyn\csc.exe" 2^>nul') do set "CSC=%%i"
)

if "%CSC%"=="" (
    echo ERROR: Cannot find Roslyn csc.exe. Install Visual Studio or .NET SDK.
    exit /b 1
)

echo Using compiler: %CSC%

set REF=C:\PROGRA~2\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8

if not exist "%REF%\mscorlib.dll" (
    echo ERROR: .NET Framework 4.8 reference assemblies not found at %REF%
    exit /b 1
)

cd /d "%~dp0UltraLightApiTester"

echo.
echo ================================
echo  Building UltraLightApiTester...
echo ================================

set REFS=
set REFS=%REFS% -reference:"%REF%\mscorlib.dll"
set REFS=%REFS% -reference:"%REF%\System.dll"
set REFS=%REFS% -reference:"%REF%\System.Windows.Forms.dll"
set REFS=%REFS% -reference:"%REF%\System.Drawing.dll"
set REFS=%REFS% -reference:"%REF%\System.Net.Http.dll"
set REFS=%REFS% -reference:"%REF%\System.Core.dll"
set REFS=%REFS% -reference:"%REF%\System.Web.Extensions.dll"

"%CSC%" ^
  -target:winexe ^
  -out:UltraLightApiTester.exe ^
  %REFS% ^
  -optimize+ ^
  -debug- ^
  -langversion:7.3 ^
  -nologo ^
  Program.cs ^
  Models\SavedRequest.cs ^
  Models\SwaggerModels.cs ^
  Models\EnvironmentConfig.cs ^
  Services\JsonStore.cs ^
  Services\HttpSingleton.cs ^
  Services\HistoryService.cs ^
  Services\FavoriteService.cs ^
  Services\EnvironmentService.cs ^
  Services\SwaggerParser.cs ^
  UI\SwaggerImportDialog.cs ^
  UI\EnvironmentDialog.cs ^
  UI\MainForm.cs

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ================================
    echo  Build FAILED
    echo ================================
    exit /b 1
)

echo.
echo ================================
echo  Build SUCCESS
echo ================================
for %%A in (UltraLightApiTester.exe) do echo Size: %%~zA bytes

endlocal
