@echo off
setlocal EnableExtensions
pushd "%~dp0"

if not exist "bin\ARTBatchEncoder.exe" (
    call build.bat
    if errorlevel 1 (
        popd
        exit /b 1
    )
)

if not exist "bin\artbe_settings.ini" copy /y "artbe_settings.ini" "bin\artbe_settings.ini" >nul
if not exist "bin\ffmpeg" mkdir "bin\ffmpeg"
if not exist "bin\ffmpeg\README.txt" if exist "ffmpeg\README.txt" copy /y "ffmpeg\README.txt" "bin\ffmpeg\README.txt" >nul
if exist "ffmpeg\ffmpeg.exe" if not exist "bin\ffmpeg\ffmpeg.exe" copy /y "ffmpeg\ffmpeg.exe" "bin\ffmpeg\ffmpeg.exe" >nul
if not exist "bin\openimageio" mkdir "bin\openimageio"
if not exist "bin\openimageio\README.txt" if exist "openimageio\README.txt" copy /y "openimageio\README.txt" "bin\openimageio\README.txt" >nul
if exist "openimageio\oiiotool.exe" xcopy /e /i /y "openimageio\*" "bin\openimageio\" >nul
if exist "openimageio\bin\oiiotool.exe" xcopy /e /i /y "openimageio\*" "bin\openimageio\" >nul

start "" "bin\ARTBatchEncoder.exe"
popd
