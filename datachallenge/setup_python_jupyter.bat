@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

title Python 3.12.10 + Jupyter 자동 설치

echo.
echo  ╔══════════════════════════════════════════════════════════╗
echo  ║     Python 3.12.10 + 가상환경 + Jupyter 자동 설치      ║
echo  ╚══════════════════════════════════════════════════════════╝
echo.

:: ── 설정 ───────────────────────────────────────────────────────
set PYTHON_VERSION=3.12.10
set PYTHON_INSTALLER=python-%PYTHON_VERSION%-amd64.exe
set PYTHON_URL=https://www.python.org/ftp/python/%PYTHON_VERSION%/%PYTHON_INSTALLER%
set PYTHON_TEMP=%TEMP%\%PYTHON_INSTALLER%
set VENV_NAME=main
:: ──────────────────────────────────────────────────────────────


:: ═══════════════════════════════════════════════════════════════
:: STEP 1. Python 3.12.10 다운로드
:: ═══════════════════════════════════════════════════════════════
echo  [STEP 1/6] Python %PYTHON_VERSION% 다운로드 중...
echo  URL : %PYTHON_URL%
echo.

curl -L --progress-bar -o "%PYTHON_TEMP%" "%PYTHON_URL%"

if %errorlevel% neq 0 (
    echo.
    echo  [오류] 다운로드 실패. 인터넷 연결을 확인하세요.
    pause & exit /b 1
)
echo.
echo  [OK] 다운로드 완료!


:: ═══════════════════════════════════════════════════════════════
:: STEP 2. Python 설치  (Add python.exe to PATH 포함)
:: ═══════════════════════════════════════════════════════════════
echo.
echo  [STEP 2/6] Python %PYTHON_VERSION% 설치 중...
echo  (Add python.exe to PATH 옵션 자동 체크 적용)
echo.

"%PYTHON_TEMP%" /quiet ^
    InstallAllUsers=0 ^
    PrependPath=1 ^
    Include_pip=1 ^
    Include_launcher=1 ^
    Include_test=0 ^
    Shortcuts=0

if %errorlevel% neq 0 (
    echo  [오류] Python 설치 실패 (종료 코드: %errorlevel%)
    pause & exit /b 1
)
echo  [OK] Python %PYTHON_VERSION% 설치 완료!

:: 설치 후 PATH 즉시 반영
for /f "usebackq tokens=2,*" %%A in (
    `reg query "HKCU\Environment" /v PATH 2^>nul`
) do set "USER_PATH=%%B"
set "PATH=%USER_PATH%;%PATH%"

where python >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo  [경고] PATH가 아직 반영되지 않았습니다.
    echo         이 창을 닫고 새 명령 프롬프트에서 다시 실행하세요.
    pause & exit /b 1
)

echo.
python --version


:: ═══════════════════════════════════════════════════════════════
:: STEP 3. pip 업그레이드
:: ═══════════════════════════════════════════════════════════════
echo.
echo  [STEP 3/6] pip 업그레이드 중...
echo  ^> pip install pip --upgrade
echo.

pip install pip --upgrade

if %errorlevel% neq 0 (
    echo  [경고] pip 업그레이드 실패. 계속 진행합니다.
) else (
    echo.
    echo  [OK] pip 업그레이드 완료!
    pip --version
)


:: ═══════════════════════════════════════════════════════════════
:: STEP 4. 가상환경 생성
:: ═══════════════════════════════════════════════════════════════
echo.
echo  [STEP 4/6] 가상환경 생성 중...
echo  ^> python -m venv main
echo.

if exist "main" (
    echo  [정보] 이미 "main" 폴더가 존재합니다. 삭제 후 재생성합니다.
    rmdir /s /q "main"
)

python -m venv main

if %errorlevel% neq 0 (
    echo  [오류] 가상환경 생성 실패
    pause & exit /b 1
)
echo  [OK] 가상환경 "main" 생성 완료!


:: ═══════════════════════════════════════════════════════════════
:: STEP 5. 가상환경 활성화 + Jupyter 설치 + pip 재업그레이드
:: ═══════════════════════════════════════════════════════════════
echo.
echo  [STEP 5/6] 가상환경 활성화 및 Jupyter 설치 중...
echo  ^> main\Scripts\activate
echo.

call "main\Scripts\activate.bat"

if %errorlevel% neq 0 (
    echo  [오류] 가상환경 활성화 실패
    pause & exit /b 1
)
echo  [OK] 가상환경 활성화됨: (main)
echo.

echo  ^> pip install jupyter
pip install jupyter

if %errorlevel% neq 0 (
    echo  [오류] Jupyter 설치 실패
    pause & exit /b 1
)
echo.
echo  [OK] Jupyter 설치 완료!
echo.

echo  ^> python.exe -m pip install --upgrade pip
python.exe -m pip install --upgrade pip

echo.
echo  [OK] pip 최종 업그레이드 완료!
pip --version


:: ═══════════════════════════════════════════════════════════════
:: STEP 6. Jupyter Notebook 실행
:: ═══════════════════════════════════════════════════════════════
echo.
echo  ╔══════════════════════════════════════════════════════════╗
echo  ║                    설치 완료!                           ║
echo  ╠══════════════════════════════════════════════════════════╣
echo  ║  Python  : 3.12.10
echo  ║  가상환경: main
echo  ║  Jupyter : 설치 완료
echo  ╚══════════════════════════════════════════════════════════╝
echo.
echo  [STEP 6/6] Jupyter Notebook 실행 중...
echo  ^> jupyter notebook
echo.

jupyter notebook

endlocal
pause
