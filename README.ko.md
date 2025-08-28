# Symphony
`Symphony`는 `VALOFE` 의 게임 `LastOrigin` PC 클라이언트를 위한 [BepInEx](https://github.com/BepInEx/BepInEx) 플러그인입니다.
각종 사용자 편의 기능(QoL)을 제공하는 것을 목표로 하고 있습니다.

## 설치 방법
1. [BepInEx](https://github.com/BepInEx/BepInEx) 6의 **BepInEx-Unity.Mono-win-x64** 버전을  [다운로드 페이지](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2)에서 다운로드합니다.
2. 압축 파일을 풀어 `LastOrigin.exe` 파일이 존재하는 설치 폴더에 붙여넣기 합니다.\
이 때, `winhttp.dll` 파일과 `LastOrigin.exe` 파일이 같은 폴더에 존재해야합니다.
3. `BepInEx` 폴더 내의 `plugins` 안에 `Symphony.dll` 을 넣습니다.

## 사용 방법
플러그인 내 각 기능별 설명입니다.

### WindowedResize
`Alt+Enter` 입력을 통한 창모드 전환 후, 창 크기 조절 가능 및 조절한 위치 및 크기를 기억하는 기능입니다.\
사용자는 설정할 내용이 없습니다.

만약 <ins>창이 화면 밖으로 벗어나는 등</ins>의 문제가 발생하여 설정을 초기화해야하는 경우, `BepInEx/config` 폴더 내의 `Symphony.WindowedResize.cfg` 파일을 삭제 후 게임을 재시작하면 해결됩니다.

## 업데이트
플러그인에 업데이트가 필요한 경우, 게임 시작 시 다음과 같은 메시지가 출력됩니다.

![Update Screen](doc/update.png)

`[이동하기]` 버튼을 누르면 자동으로 이 리포지터리의 Releases 페이지로 이동됩니다.

## 책임 및 경고
이 모드는 비공식적으로 제작되었으며, 사용으로 인해 발생하는 모든 문제와 책임은 전적으로 사용자 본인에게 있습니다. 모드 사용 전, 중요한 데이터는 반드시 백업하시기 바랍니다. 개발자는 이 모드의 사용으로 인한 어떠한 손실이나 손해에 대해서도 책임지지 않습니다.

## 라이센스
`Symphony` 프로젝트는 `LGPL-2.1 라이센스` 하에 있습니다.
