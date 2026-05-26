@echo off
title AllRename — Build
chcp 65001 > NUL
setlocal

set APP_NAME=AllRename
set APP_VERSION=1.1.0
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set TARGET=%1
if "%TARGET%"=="" set TARGET=portable

echo.
echo ╔══════════════════════════════════════╗
echo ║   AllRename v%APP_VERSION% — Build System  ║
echo ╚══════════════════════════════════════╝
echo.

:: ─── Kill instances ───────────────────────────────────────────────
echo [1/4] Arrêt des instances en cours...
taskkill /F /IM %APP_NAME%.exe 2>NUL
timeout /t 1 /nobreak > NUL

:: ─── Nettoyage ────────────────────────────────────────────────────
echo [2/4] Nettoyage des artefacts...
if exist ".\publish"              rmdir /S /Q ".\publish"
if exist ".\bin\Release"          rmdir /S /Q ".\bin\Release"
if exist ".\installer\output"     rmdir /S /Q ".\installer\output"

:: ─── Portable ─────────────────────────────────────────────────────
if "%TARGET%"=="portable" goto :build_portable
if "%TARGET%"=="installer" goto :build_installer
if "%TARGET%"=="all" goto :build_all
echo [ERREUR] Cible inconnue: %TARGET%  (utiliser: portable / installer / all)
exit /b 1

:build_portable
echo [3/4] Publication Single File portable...
dotnet publish -c Release -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeAllContentForSelfExtract=true ^
    /p:PublishReadyToRun=true ^
    -o ".\publish"
if %ERRORLEVEL% neq 0 goto :error

:: Renommer avec version pour la release
copy /Y ".\publish\%APP_NAME%.exe" ".\publish\%APP_NAME%_v%APP_VERSION%_Portable.exe" > NUL
echo.
echo [OK] Portable → .\publish\%APP_NAME%_v%APP_VERSION%_Portable.exe
goto :done

:build_installer
echo [3/4] Publication + création de l'installateur...
:: Publish d'abord
dotnet publish -c Release -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeAllContentForSelfExtract=true ^
    /p:PublishReadyToRun=true ^
    -o ".\publish"
if %ERRORLEVEL% neq 0 goto :error

:: Compiler l'installateur Inno Setup
echo [4/4] Compilation installateur Inno Setup...
mkdir ".\installer\output" 2>NUL
%ISCC% ".\installer\AllRename.iss"
if %ERRORLEVEL% neq 0 (
    echo [ERREUR] Inno Setup a échoué. Vérifier C:\Program Files ^(x86^)\Inno Setup 6\
    goto :error
)
echo.
echo [OK] Installateur → .\installer\output\%APP_NAME%_v%APP_VERSION%_Setup.exe
goto :done

:build_all
echo [3/4] Build portable + installateur + release GitHub...
:: Portable
dotnet publish -c Release -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeAllContentForSelfExtract=true ^
    /p:PublishReadyToRun=true ^
    -o ".\publish"
if %ERRORLEVEL% neq 0 goto :error
copy /Y ".\publish\%APP_NAME%.exe" ".\publish\%APP_NAME%_v%APP_VERSION%_Portable.exe" > NUL

:: Installateur
mkdir ".\installer\output" 2>NUL
%ISCC% ".\installer\AllRename.iss"
if %ERRORLEVEL% neq 0 goto :error

:: Release GitHub (gh CLI requis)
where gh > NUL 2>&1
if %ERRORLEVEL% neq 0 (
    echo [WARN] gh CLI non trouvé — release GitHub ignorée.
    goto :done
)
echo [4/4] Création de la release GitHub v%APP_VERSION%...
gh release create "v%APP_VERSION%" ^
    ".\publish\%APP_NAME%_v%APP_VERSION%_Portable.exe#AllRename v%APP_VERSION% Portable" ^
    ".\installer\output\%APP_NAME%_v%APP_VERSION%_Setup.exe#AllRename v%APP_VERSION% Installateur" ^
    --title "AllRename v%APP_VERSION%" ^
    --notes-file "RELEASE_NOTES.md"
goto :done

:error
echo.
echo [ERREUR] Build échoué avec code %ERRORLEVEL%.
pause
exit /b %ERRORLEVEL%

:done
echo.
echo ══════════════════════════════════════════
echo  Build terminé avec succès.
echo ══════════════════════════════════════════
echo.
pause
endlocal
