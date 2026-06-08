@echo off
cd /d "%~dp0"
dotnet build HyperPet.sln
start "" "src\HyperPet.App\bin\Debug\net8.0-windows10.0.19041.0\HyperPet.exe"
