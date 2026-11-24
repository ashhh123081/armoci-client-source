@echo off
setlocal
pushd "%~dp0.."
call gradlew.bat -p fabric localRelease --stacktrace
set "result=%ERRORLEVEL%"
popd
exit /b %result%
