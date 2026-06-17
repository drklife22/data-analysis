@echo off
setlocal EnableDelayedExpansion

title Python 3.12.10 + Jupyter Setup

echo.
echo  ================================================
echo   Python 3.12.10 + venv + Jupyter Auto Install
echo  ================================================
echo.

set PYTHON_URL=https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe
set PYTHON_TEMP=%TEMP%\python-3.12.10-amd64.exe

:: STEP 1. Download Python 3.12.10
echo [STEP 1/6] Downloading Python 3.12.10...
echo.

curl -k -L --progress-bar -o "%PYTHON_TEMP%" "%PYTHON_URL%"

if %errorlevel% neq 0 (
    echo [ERROR] Download failed. Check internet connection.
    pause
    exit /b 1
)
echo [OK] Download complete!

:: STEP 2. Install Python with PATH option
echo.
echo [STEP 2/6] Installing Python 3.12.10...
echo (Add python.exe to PATH = enabled)
echo.

"%PYTHON_TEMP%" /quiet InstallAllUsers=0 PrependPath=1 Include_pip=1 Include_launcher=1 Include_test=0 Shortcuts=0

if %errorlevel% neq 0 (
    echo [ERROR] Python install failed. Code: %errorlevel%
    pause
    exit /b 1
)
echo [OK] Python 3.12.10 installed!

:: Refresh PATH from registry
for /f "usebackq tokens=2,*" %%A in (`reg query "HKCU\Environment" /v PATH 2^>nul`) do set "USER_PATH=%%B"
set "PATH=%USER_PATH%;%PATH%"

echo.
python --version

:: STEP 3. Upgrade pip
echo.
echo [STEP 3/6] Upgrading pip...
echo ^> pip install pip --upgrade
echo.

pip install pip --upgrade

if %errorlevel% neq 0 (
    echo [WARN] pip upgrade failed. Continuing...
) else (
    echo [OK] pip upgraded!
    pip --version
)

:: STEP 4. Create virtual environment
echo.
echo [STEP 4/6] Creating virtual environment...
echo ^> python -m venv main
echo.

if exist "main" (
    echo [INFO] main folder exists. Removing and recreating...
    rmdir /s /q "main"
)

python -m venv main

if %errorlevel% neq 0 (
    echo [ERROR] venv creation failed.
    pause
    exit /b 1
)
echo [OK] Virtual environment main created!

:: STEP 5. Activate + Install Jupyter + Upgrade pip
echo.
echo [STEP 5/6] Activating venv and installing Jupyter...
echo ^> main\Scripts\activate
echo.

call "main\Scripts\activate.bat"

if %errorlevel% neq 0 (
    echo [ERROR] venv activation failed.
    pause
    exit /b 1
)
echo [OK] venv activated: (main)
echo.

echo ^> pip install jupyter
pip install jupyter

if %errorlevel% neq 0 (
    echo [ERROR] Jupyter install failed.
    pause
    exit /b 1
)
echo [OK] Jupyter installed!
echo.

echo ^> python.exe -m pip install --upgrade pip
python.exe -m pip install --upgrade pip

echo [OK] pip upgrade complete!
pip --version

:: STEP 6. Launch Jupyter Notebook
echo.
echo  ================================================
echo   Install Complete!
echo   Python  : 3.12.10
echo   venv    : main
echo   Jupyter : installed
echo  ================================================
echo.
echo [STEP 6/6] Launching Jupyter Notebook...
echo ^> jupyter notebook
echo.

jupyter notebook

endlocal
pause
