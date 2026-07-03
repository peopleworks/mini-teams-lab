@echo off
REM Simulador de Equipo para el mini-Teams.
REM Uso:  doble clic  ->  usa http://localhost:5000
REM   o:  Correr-Simulador.cmd http://localhost:5099   (pon el puerto real de tu app)
cd /d "%~dp0"
set API=%1
if "%API%"=="" set API=http://localhost:5000
echo Conectando el equipo simulado a  %API%
dotnet run -- --api %API%
pause
