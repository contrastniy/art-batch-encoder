@echo off
setlocal EnableExtensions
pushd "%~dp0"

if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "dist" rmdir /s /q "dist"

echo ART Batch Encoder build and release output removed.
popd
