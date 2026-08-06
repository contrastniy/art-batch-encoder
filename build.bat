@echo off
setlocal EnableExtensions
pushd "%~dp0"

set "APP_NAME=ART Batch Encoder"
set "OUTPUT_EXE=%CD%\bin\ARTBatchEncoder.exe"
set "BUILD_OK="

if not exist "bin" mkdir "bin"

rem The .NET Framework compiler is the most portable build path on Windows.
rem It does not require a Visual Studio installation or NuGet restore.
set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if defined CSC call :build_with_csc
if defined BUILD_OK goto :stage_runtime

rem Fall back to Visual Studio MSBuild when the framework compiler is missing
rem or when a newer compiler is required by the local toolchain.
set "MSBUILD="
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
    for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
        if not defined MSBUILD set "MSBUILD=%%I"
    )
)
if not defined MSBUILD for /f "delims=" %%I in ('where msbuild.exe 2^>nul') do if not defined MSBUILD set "MSBUILD=%%I"

if defined MSBUILD call :build_with_msbuild
if defined BUILD_OK goto :stage_runtime

echo.
echo ERROR: %APP_NAME% could not be built.
echo.
echo Install one of the following and run build.bat again:
echo   - .NET Framework 4.8 Developer Pack, or
echo   - Visual Studio 2022 with .NET desktop development.
echo.
popd
pause
exit /b 1

:build_with_csc
set "RSP=%TEMP%\artbe_build_%RANDOM%_%RANDOM%.rsp"

>"%RSP%" echo /nologo
>>"%RSP%" echo /target:winexe
>>"%RSP%" echo /optimize+
>>"%RSP%" echo /platform:anycpu
>>"%RSP%" echo /langversion:5
>>"%RSP%" echo /utf8output
>>"%RSP%" echo /win32icon:art_batch_encoder.ico
>>"%RSP%" echo /reference:System.dll
>>"%RSP%" echo /reference:System.Core.dll
>>"%RSP%" echo /reference:System.Drawing.dll
>>"%RSP%" echo /reference:System.Management.dll
>>"%RSP%" echo /reference:System.Web.Extensions.dll
>>"%RSP%" echo /reference:System.Windows.Forms.dll
>>"%RSP%" echo /out:bin\ARTBatchEncoder.exe
>>"%RSP%" echo src\AssemblyInfo.cs
>>"%RSP%" echo src\BatchManifestReader.cs
>>"%RSP%" echo src\CodecCatalog.cs
>>"%RSP%" echo src\CodecProfile.cs
>>"%RSP%" echo src\ExrCompressionProfile.cs
>>"%RSP%" echo src\GpuSupport.cs
>>"%RSP%" echo src\MainForm.cs
>>"%RSP%" echo src\MainForm.Encoding.cs
>>"%RSP%" echo src\MainForm.ExrEncoding.cs
>>"%RSP%" echo src\MainForm.Layout.cs
>>"%RSP%" echo src\MainForm.Lifecycle.cs
>>"%RSP%" echo src\MainForm.SequenceList.cs
>>"%RSP%" echo src\MainForm.Source.cs
>>"%RSP%" echo src\ManifestReader.cs
>>"%RSP%" echo src\Models.cs
>>"%RSP%" echo src\OpenExrJob.cs
>>"%RSP%" echo src\OpenImageIoSupport.cs
>>"%RSP%" echo src\OutputMode.cs
>>"%RSP%" echo src\Program.cs
>>"%RSP%" echo src\SettingsStore.cs
>>"%RSP%" echo src\ThemeControls.cs

echo Building %APP_NAME% v1.0 with .NET Framework csc.exe...
"%CSC%" @"%RSP%"
set "CSC_EXIT=%ERRORLEVEL%"
del /q "%RSP%" >nul 2>nul

if "%CSC_EXIT%"=="0" (
    set "BUILD_OK=1"
) else (
    echo.
    echo csc.exe build failed with exit code %CSC_EXIT%.
    echo Trying MSBuild if it is available...
)
exit /b 0

:build_with_msbuild
echo.
echo Building %APP_NAME% v1.0 with MSBuild...
"%MSBUILD%" "ARTBatchEncoder.csproj" /nologo /m /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /p:SkipReleasePackaging=true
if not errorlevel 1 set "BUILD_OK=1"
exit /b 0

:stage_runtime
if not exist "%OUTPUT_EXE%" (
    echo ERROR: The compiler reported success but %OUTPUT_EXE% was not created.
    popd
    pause
    exit /b 1
)

copy /y "ARTBatchEncoder.exe.config" "bin\ARTBatchEncoder.exe.config" >nul
if not exist "bin\artbe_settings.ini" copy /y "artbe_settings.ini" "bin\artbe_settings.ini" >nul
if not exist "bin\ffmpeg" mkdir "bin\ffmpeg"
if exist "ffmpeg" xcopy /e /i /y "ffmpeg\*" "bin\ffmpeg\" >nul
if not exist "bin\openimageio" mkdir "bin\openimageio"
if exist "openimageio" xcopy /e /i /y "openimageio\*" "bin\openimageio\" >nul

echo.
echo Build complete: bin\ARTBatchEncoder.exe

echo.
echo Creating release package in dist...
set "POWERSHELL_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%POWERSHELL_EXE%" set "POWERSHELL_EXE=powershell.exe"
"%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%CD%\package.ps1" -Version "1.0"
if errorlevel 1 (
    echo.
    echo ERROR: Build succeeded, but release packaging failed.
    popd
    pause
    exit /b 1
)

echo.
echo Build and packaging complete.
echo Final release output: dist\ART_Batch_Encoder_v1.0
popd
exit /b 0
