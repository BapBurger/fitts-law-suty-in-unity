# HCI Target Acquisition Experiment (Unity + Meta Quest 3)

본 프로젝트는 **Meta Quest 3** 환경에서 Fitts' Law 기반의 **Target Acquisition 실험**을 수행하기 위한 Unity 프로젝트입니다.  
실험 참가자는 컨트롤러를 사용하여 화면에 표시되는 원형(Target)을 터치하며, 각 트라이얼별 조작 데이터가 **CSV** 및 **Google Sheets**로 기록됩니다.

---

## 🧩 주요 구성 스크립트

| 파일명 | 주요 기능 |
|---------|------------|
| `ExperimentManager.cs` | 실험 전반의 진행 제어(세션, 블록, 라운드, 트라이얼 순환, 타겟 활성화 등) |
| `TargetLayout.cs` | 11개의 타깃을 원형으로 배치 (지름 = Amplitude, 각 타깃 크기 = Width) |
| `CursorInteraction.cs` | 컨트롤러의 레이캐스트 충돌 감지 및 클릭 이벤트 처리 |
| `DataLogger.cs` | 각 트라이얼의 측정 데이터를 로컬 CSV 및 Download/HCIExp 폴더에 저장 |
| `GoogleSpreadsheetsManager.cs` | (선택) Google Form 연동: CSV 데이터를 Google Sheet로 업로드 |
| `DriveCsvUploader.cs` | (선택) Google Drive Apps Script를 통한 CSV 업로드 기능 |

---

## 🧠 실험 구조 개요

1. **Start 버튼 클릭 시 실험 시작**
   - 첫 타겟이 활성화되고, 컨트롤러의 **Trigger** 입력으로 터치 감지.
   - 터치 시 다음 타겟으로 이동.

2. **Target Layout**
   - 총 11개의 타겟이 원형으로 배치됨.
   - `Amplitude`는 원의 지름, `Width`는 타겟 지름으로 설정.
   - 타겟은 납작한 디스크 형태(`BoxCollider`, 두께 5mm)로 구성되어 있음.

3. **데이터 로깅**
   - 각 트라이얼별로 아래 항목을 기록:
     ```
     Round, ID, Amplitude(A), Width(W), MovementTime(MT), ErrorRate(ER), Throughput(TP),
     TouchPosX, TouchPosY, TouchPosZ, TargetCenterX, TargetCenterY, TargetCenterZ,
     TouchOffset, HeadMovement
     ```
   - `TouchOffset` = 실제 터치 위치와 타깃 중심 간의 거리.
   - 모든 좌표는 **World Space(절대 좌표)** 기준으로 기록됨.

4. **데이터 저장 경로**
   - 기본적으로 `Application.persistentDataPath` 에 저장.
   - Android에서는 `/sdcard/Download/HCIExp` 폴더에도 복사 시도.
   - PC에서는 `C:\Users\<사용자>\Downloads\HCIExp` 폴더로 자동 복사.

---

## 📊 Fitts' Law 변수 정의

| 변수 | 의미 | 계산 방식 |
|------|------|-----------|
| **A (Amplitude)** | 타겟 원의 지름 | TargetLayout 내 원형 배치 반지름 × 2 |
| **W (Width)** | 각 타겟의 지름 | Inspector에서 직접 지정 |
| **ID (Index of Difficulty)** | 조작 난이도 | `ID = log2(A/W + 1)` |
| **MT (Movement Time)** | 터치까지 걸린 시간(ms) | 타겟 활성화~터치 시점 |
| **TP (Throughput)** | 처리율(bits/s) | `TP = ID / (MT/1000)` |
| **ER (Error Rate)** | 오류율(%) | 터치 실패 시 1로 기록 |

---

## 🧾 Google Form / Drive 연동

### ① Google Form 업로드
- `GoogleSpreadsheetsManager` 스크립트의 `Form Response URL`에 `formResponse` 주소 입력
- 각 entry ID에 맞춰 데이터를 POST
- `Submit Interval` (기본 0.15초): 너무 짧으면 누락 발생 가능 → 0.3~0.5 권장

### ② Google Drive 업로드 (Apps Script)
- Google Apps Script를 “웹 앱”으로 배포 (`Anyone with link` + `Execute as Me`)
- 발급된 URL을 Unity `DriveCsvUploader` 컴포넌트에 입력
- CSV 파일을 업로드하면 Google Drive 지정 폴더에 저장됨

---

## 🌎 Passthrough(MR) 환경 전환 (Meta Quest 3)

1. **OVRCameraRig + OVRManager 존재 확인**
2. **OVRPassthroughLayer 추가**
   - Component → Meta XR → OVRPassthroughLayer
   - Placement = Underlay, Opacity = 1.0
3. **카메라 설정**
   - CenterEyeAnchor Camera → Clear Flags: **Solid Color**
   - Background: RGBA(0,0,0,0)
4. **Lighting 설정**
   - Window → Rendering → Lighting → Environment → **Skybox Material = None**

---

## 🕹️ 컨트롤러 인터랙션

- **Trigger 버튼** → 타겟 선택 입력
- **B 버튼** → Recenter (실험 중 재조정)
- 커서 위치는 컨트롤러 끝부분으로 Raycast가 향하도록 조정 가능:
  ```csharp
  cursorOffset = controller.forward * offsetDistance;
