@echo off
rem Builds RA3Mouse.exe from RA3Mouse.cs with the C# compiler that ships with Windows.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
cd /d "%~dp0"
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:RA3Mouse.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll RA3Mouse.cs
if errorlevel 1 (echo Build failed. & pause & exit /b 1)
echo Built RA3Mouse.exe
pause
