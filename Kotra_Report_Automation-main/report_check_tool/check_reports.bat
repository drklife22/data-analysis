@echo off
setlocal EnableExtensions
title KOTRA report check

cd /d "%~dp0"

set "ROOT=%~dp0.."
set "TOOL_DIR=%CD%"
set "RUNTIME_FILE=%ROOT%\.runtime_python.bat"
set "PYTHON_EXE=%ROOT%\portable_python\python.exe"
set "SCRIPT=%TOOL_DIR%\check_reports_against_input.py"
if exist "%RUNTIME_FILE%" call "%RUNTIME_FILE%"

echo ==========================================
echo KOTRA report check
echo ==========================================
echo Tool folder:
echo %TOOL_DIR%
echo.

if not exist "%PYTHON_EXE%" (
    echo Python runtime was not found.
    echo Run install_vm.bat in the main program folder first.
    set "EXIT_CODE=1"
    goto END
)

if not exist "%SCRIPT%" (
    echo Check script was not found.
    echo %SCRIPT%
    set "EXIT_CODE=1"
    goto END
)

echo Running check...
echo.
"%PYTHON_EXE%" "%SCRIPT%" --folder "%TOOL_DIR%" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if errorlevel 1 (
    echo Report check failed. See the message above.
) else (
    echo Report check finished.
)

:END
echo.
pause
endlocal
exit /b %EXIT_CODE%
