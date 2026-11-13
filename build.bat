@echo off
cd /d "%~dp0"
dotnet build HyperPet.sln
if errorlevel 1 exit /b %errorlevel%
dotnet run --no-build --project src\HyperPet.App\HyperPet.App.csproj
