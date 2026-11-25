@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo === [1/3] Build ban moi nhat ===
call build.bat
if errorlevel 1 exit /b 1

echo === [2/3] Dong goi ban sach 100%% (KHONG kem data/ va rms dang nhap) ===
set STAGE=release\ModX_HTeam_AutoSKH
set ZIP=release\ModX_HTeam_AutoSKH_Public.zip
if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%ZIP%" del /f /q "%ZIP%"
mkdir "%STAGE%"

copy /Y "ModX_HTeam_AutoSKH.exe" "%STAGE%\" >nul
copy /Y "README.md" "%STAGE%\" >nul
copy /Y "HDSD.md" "%STAGE%\" >nul
copy /Y "HDSD.txt" "%STAGE%\" >nul
xcopy /E /I /Y /Q "game" "%STAGE%\game" >nul

REM Xoa sach moi file dang nhap / log tam truoc khi nen share
del /f /q "%STAGE%\game\Data\rms\acc" 2>nul
del /f /q "%STAGE%\game\Data\rms\pass" 2>nul
del /f /q "%STAGE%\game\Data\rms\userAo*" 2>nul
del /f /q "%STAGE%\game\Dragonboy_vn_v231_Data\output_log.txt" 2>nul
del /f /q "%STAGE%\game\UpdateChar.txt" 2>nul
del /f /q "%STAGE%\game\ReadChar.txt" 2>nul

echo === [3/3] Nen file ZIP phat hanh ===
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 exit /b 1

echo === XONG! File share sach 100%% tai: %ZIP% ===
