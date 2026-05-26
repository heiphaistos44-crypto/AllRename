@echo off
title AllRename — Build
chcp 65001 > NUL

echo [BUILD] Arrêt des instances en cours...
taskkill /F /IM AllRename.exe 2>NUL

echo [BUILD] Nettoyage artefacts...
if exist ".\publish" rmdir /S /Q ".\publish"
if exist ".\bin\Release" rmdir /S /Q ".\bin\Release"

echo [BUILD] Publication Single File .exe...
dotnet publish -c Release -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeAllContentForSelfExtract=true ^
    /p:PublishReadyToRun=true ^
    -o ".\publish"

if %ERRORLEVEL% neq 0 (
    echo [ERREUR] Build échoué avec code %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [OK] Build réussi.
echo [OK] Executable : .\publish\AllRename.exe
echo.
pause
