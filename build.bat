@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set GO_BIN=%USERPROFILE%\sdk\go1.20.14\bin\go.exe
if not exist "%GO_BIN%" set GO_BIN=go

echo === [1/4] Chay Unit Test Golang (%GO_BIN%) ===
"%GO_BIN%" test -count=1 ./...
if errorlevel 1 exit /b 1

echo === [2/4] Build Client Mod C# (Assembly-CSharp.dll) ===
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set MSBUILD=%%i
if not defined MSBUILD (
  echo Khong tim thay MSBuild cua Visual Studio!
  exit /b 1
)
"%MSBUILD%" "client\AssemblyCSharp\AssemblyCSharp.csproj" /restore /p:Configuration=Release /t:Rebuild /v:minimal /clp:ErrorsOnly
if errorlevel 1 exit /b 1
if exist "client\AssemblyCSharp\bin\Release\net35\Assembly-CSharp.dll" (
  copy /Y "client\AssemblyCSharp\bin\Release\net35\Assembly-CSharp.dll" "game\Dragonboy_vn_v231_Data\Managed\Assembly-CSharp.dll" >nul
)

echo === [3/4] Nhung Icon MOD X - H TEAM va Manifest (go-winres) ===
set WINRES_BIN=%USERPROFILE%\go\bin\go-winres.exe
if not exist "%WINRES_BIN%" set WINRES_BIN=go-winres
"%WINRES_BIN%" make --in winres\winres.json --arch amd64
if errorlevel 1 exit /b 1

echo === [4/4] Build ModX_HTeam_AutoSKH.exe (Go 1.20.14 - Win Server 2012 R2 den Win 11) ===
"%GO_BIN%" build -trimpath -ldflags "-w -s -H windowsgui" -o ModX_HTeam_AutoSKH.exe .
if errorlevel 1 exit /b 1

echo === HOAN TAT! Mo ModX_HTeam_AutoSKH.exe de su dung ===
