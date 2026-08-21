@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul

rem ===========================================================
rem  안드로이드 기기 로그를 Logs\device.log 로 저장한다.
rem
rem  쓰는 법
rem    1. 폰에서 개발자 옵션 > USB 디버깅 켜고 USB 연결
rem       (연결 시 폰에 뜨는 "USB 디버깅을 허용하시겠습니까?" 를 반드시 허용)
rem    2. 이 파일을 더블클릭
rem    3. 앱이 자동으로 실행된다. 문제 상황을 재현한다
rem    4. 이 창에서 Ctrl+C 를 눌러 저장을 끝낸다
rem
rem  저장 위치: <프로젝트>\Logs\device.log
rem ===========================================================

set "PKG=com.pipie.matblast"
set "OUT=%~dp0..\Logs\device.log"

rem ── adb 찾기: PATH 우선, 없으면 Unity 에디터에 딸린 것 ──────
set "ADB="
where adb >nul 2>&1 && set "ADB=adb"

if not defined ADB (
  for /d %%D in ("B:\Unity\Hub\Editor\*") do (
    if exist "%%D\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" (
      set "ADB=%%D\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
    )
  )
)

if not defined ADB (
  echo [오류] adb 를 찾지 못했습니다.
  echo        Unity 설치 경로가 B:\Unity\Hub\Editor 가 아니면 이 파일의 경로를 고쳐 주세요.
  pause
  exit /b 1
)

echo adb: %ADB%
echo.

rem ── 기기 확인 ───────────────────────────────────────────────
echo [1/4] 연결된 기기 확인
"%ADB%" devices
echo.
echo 위 목록에 기기가 "device" 로 보이지 않으면 중단하고 USB 디버깅을 확인하세요.
echo   - "unauthorized" : 폰 화면의 허용 팝업을 눌러 주세요
echo   - 목록이 비어 있음 : 케이블/포트를 바꿔 보세요 (충전 전용 케이블은 안 됩니다)
echo.
pause

rem ── 로그 비우고 앱 재시작 ───────────────────────────────────
echo [2/4] 이전 로그 지우기
"%ADB%" logcat -c

echo [3/4] 앱 재시작
"%ADB%" shell am force-stop %PKG%
"%ADB%" shell monkey -p %PKG% -c android.intent.category.LAUNCHER 1 >nul 2>&1

if not exist "%~dp0..\Logs" mkdir "%~dp0..\Logs"

echo.
echo [4/4] 로그 저장 중... 문제를 재현한 뒤 이 창에서 Ctrl+C 를 누르세요.
echo       저장 위치: %OUT%
echo.

"%ADB%" logcat -v time > "%OUT%"

endlocal
