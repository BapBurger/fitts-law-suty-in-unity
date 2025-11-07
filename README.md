# Fitts's Law VR/MR 실험 프레임워크 (Unity)

이 프로젝트는 Unity 환경에서 Fitts's Law 모델을 검증하기 위한 VR/MR(가상/혼합 현실) 실험을 수행할 수 있도록 설계된 종합 프레임워크입니다. Meta Quest 기기에 최적화되어 있으며, 실험 설정, 타겟 생성, 컨트롤러 상호작용, 그리고 로컬 및 클라우드(Google Drive)로의 자동 데이터 로깅까지의 전체 과정을 지원합니다.

## 🚀 주요 기능

* [cite_start]**Fitts's Law 실험 설계**: `ExperimentManager` 인스펙터 창에서 (A)진폭, (W)타겟 폭, 라운드 반복 횟수를 쉽게 설정할 수 있습니다. [cite: 33, 34]
* **동적 타겟 레이아웃**: 지정된 A, W 값에 따라 원형 Fitts's Law 타겟 레이아웃을 자동으로 생성합니다 (`TargetLayout.cs`).
* **VR 컨트롤러 상호작용**: VR 컨트롤러를 사용한 커서 기반의 상호작용(`CursorInteraction.cs`)을 지원하며, 트리거 버튼으로 타겟을 선택합니다.
* **포괄적인 데이터 로깅**: 각 트라이얼(Trial)마다 MT(이동 시간), Error Rate, Throughput(TP), 헤드 움직임(Head Movement), 터치 좌표 등 상세한 데이터를 수집합니다 (`DataLogger.cs`).
* **자동 데이터 내보내기 (다중 경로)**:
    1.  **로컬 저장**: 실험 세션 종료 시 앱 내부 저장소(`Application.persistentDataPath`)에 `.csv` 파일로 자동 저장됩니다.
    2.  **Downloads 폴더 (Android)**: 최신 Android(API 29+)와 호환되는 방식으로 기기의 'Downloads/HCIExp' 폴더에 .csv 파일을 복사합니다 (`AndroidDownloadsExporter.cs`).
    3.  **Google Drive 업로드**: Google Apps Script를 통해 세션 종료 시 수집된 `.csv` 파일 전체를 지정된 Google Drive 폴더로 자동 업로드합니다 (`DriveCsvUploader.cs`, `ExperimentManager.cs`).
* **(대안) Google Sheets 실시간 로깅**: 각 트라이얼이 끝날 때마다 Google Form을 통해 Google Sheets에 데이터를 실시간으로 전송하는 대안적인 로깅 방식(`GoogleSpreadsheetsManager.cs`)을 포함합니다.

## 🗂️ 프로젝트 구성 요소

### 1. 코어 실험 관리

* **`ExperimentManager.cs`**:
    * 실험의 "두뇌" 역할을 합니다. 라운드, 블록(A/W 조합), 트라이얼 순서를 관리합니다.
    * [cite_start]인스펙터에서 `awList` (진폭, 폭)를 설정받아 실험을 구성합니다. [cite: 33]
    * 트라이얼 완료 시(`CompleteCurrentTrial`) `DataLogger`에 데이터를 추가합니다.
    * 실험 종료 시(`EndExperiment`) `DataLogger.SaveToCSV()`를 호출해 파일을 저장하고, 이어서 `DriveCsvUploader`를 실행해 업로드를 시도합니다.
* **`TargetLayout.cs`**:
    * `ExperimentManager`로부터 A(직경), W(폭) 값을 받아 원형으로 타겟들을 배치합니다 (`PositionObjectsInCircle`).

### 2. 상호작용 (Interaction)

* **`CursorInteraction.cs`**:
    * VR 컨트롤러(`controllerTransform`)의 위치를 실시간으로 추적하는 커서 역할을 합니다.
    * OVRInput의 `PrimaryIndexTrigger` (선택) 및 `Button.Two` (실험판 재배치) 입력을 처리합니다.
    * `OnTriggerEnter` / `OnTriggerExit`를 통해 타겟 및 시작 버튼과의 충돌을 감지합니다.
* **`StartButton.cs`**:
    * 실험 시작 버튼 프리팹에 부착되어 `ExperimentManager.StartExperiment()`를 호출합니다.
* **`Target.cs`**:
    * 타겟 프리팹에 부착되어 해당 오브젝트가 '타겟'임을 식별하는 마커(표식) 역할을 합니다.

### 3. 데이터 파이프라인 (Data Pipeline)

* **`DataLogger.cs`**:
    * `TrialData` 구조체를 사용하여 각 트라이얼의 데이터를 메모리 리스트(`trialDataList`)에 누적합니다.
    * `SaveToCSV` 메서드가 호출되면(실험 종료 시), 누적된 모든 데이터를 `.csv` 파일로 생성하여 로컬 경로 및 Downloads 폴더에 저장/복사합니다.
* **`DriveCsvUploader.cs`**:
    * `DataLogger`가 생성한 로컬 `.csv` 파일 경로를 받아, Google Apps Script Web App URL로 POST 요청을 보내 파일을 업로드합니다.
    * [cite_start]`webAppUrl`, `secretKey`, `folderId`를 인스펙터에서 설정해야 합니다. [cite: 20]
* **`AndroidDownloadsExporter.cs`**:
    * Android 10 (API 29) 이상에서 Scoped Storage 정책을 준수하며 `Downloads` 폴더에 파일을 저장할 수 있도록 `MediaStore` API를 사용하는 유틸리티 스크립트입니다.
* **`GoogleSpreadsheetsManager.cs`**:
    * ***(대안 로거)*** `DriveCsvUploader`와 달리, `.csv` 파일이 아닌 각 트라이얼 데이터를 Google Form 응답 URL로 전송합니다.
    * **참고**: 현재 `ExperimentManager`는 이 스크립트를 직접 호출하지 않습니다.

### 4. 프리팹 및 씬 (Prefabs & Scene)

* **`TargetPrefabSphere.prefab`**:
    * [cite_start]`Target.cs` 스크립트가 부착되어 있습니다. [cite: 4]
    * [cite_start]`SphereCollider` (Is Trigger=true)와 `Rigidbody` (Is Kinematic=true)를 포함하여 `CursorInteraction`이 감지할 수 있도록 설정되어 있습니다. [cite: 4, 5]
* **`StartButton.prefab`**:
    * [cite_start]`StartButton.cs` 스크립트가 부착되어 있습니다. [cite: 69]
    * [cite_start]`BoxCollider` (Is Trigger=true)를 포함합니다. [cite: 69]
* **`SampleScene.unity`**:
    * 프로젝트의 모든 구성 요소가 배치된 메인 씬입니다.
    * `OVRCameraRig` (Passthrough 활성화됨), `ExperimentManager`, `TargetLayout`, `Cursor`, `csvmanager` (`DriveCsvUploader` 컴포넌트 포함) 등이 올바르게 연결되어 있습니다.

## 🛠️ 설정 및 사용법

### 1. 필수 구성 요소 (Dependencies)

* **Oculus Integration SDK**: Meta Quest의 `OVRCameraRig`, `OVRInput` 등을 사용하므로, Unity Asset Store에서 **Oculus Integration** 패키지를 임포트해야 합니다.
* **Google Apps Script (서버 측)**: `DriveCsvUploader.cs`를 사용하려면, POST 요청으로 `octet-stream` 데이터를 받아 Google Drive에 파일로 저장하는 **Google Apps Script Web App**을 별도로 배포해야 합니다.

### 2. 씬(Scene) 설정

`SampleScene.unity` 파일을 참고하여 씬을 구성합니다.

1.  **`ExperimentManager`**:
    * [cite_start]`Target Layout`: 씬의 `TargetLayout` 오브젝트를 연결합니다. [cite: 33]
    * [cite_start]`Start Button Prefab`: `StartButton.prefab` 파일을 연결합니다. [cite: 33, 66]
    * [cite_start]`Main Camera`: `OVRCameraRig/TrackingSpace/CenterEyeAnchor`의 Camera를 연결합니다. [cite: 33, 59]
    * [cite_start]`Rounds` 및 `Aw List` (진폭, 폭)를 인스펙터에서 원하는 값으로 설정합니다. [cite: 33, 34]
2.  **`TargetLayout`**:
    * [cite_start]`Target Prefab`: `TargetPrefabSphere.prefab` 파일을 연결합니다. [cite: 22, 1]
3.  **`Cursor`**:
    * [cite_start]`CursorInteraction.cs` 컴포넌트를 부착합니다. [cite: 13]
    * [cite_start]`Controller Transform`: `OVRCameraRig/TrackingSpace/RightHandAnchor` (또는 `LeftHandAnchor`)를 연결합니다. [cite: 49, 59]
    * [cite_start]`Rigidbody` (IsKinematic=true, UseGravity=false)와 `SphereCollider` (Is Trigger=true)를 부착합니다. [cite: 45, 48]
4.  **`csvmanager`**:
    * [cite_start]`DriveCsvUploader.cs` 컴포넌트를 부착합니다. [cite: 20]
    * [cite_start]인스펙터에서 **`Web App Url`**, **`Secret Key`**, **`Folder Id`**를 1-2 단계에서 만든 Google Apps Script 정보로 채워야 합니다. [cite: 20]

### 3. (대안) Google Sheets 로깅 설정

만약 세션 `.csv` 파일 대신 트라이얼별 실시간 로깅을 원한다면:

1.  **Google Form 생성**: `GoogleSpreadsheetsManager.cs`의 15개 항목에 해당하는 Google Form을 생성합니다.
2.  **`GoogleSpreadsheetsManager` 설정**: 씬에 오브젝트를 추가하고 스크립트를 부착한 뒤, `Form Response Url`을 입력합니다.
3.  **Entry Key 수정**: `GoogleSpreadsheetsManager.cs` 스크립트를 열고, `const string kRound = "entry.xxxx"` 부분의 ID 값들을 1단계에서 만든 폼의 실제 Entry ID로 모두 교체해야 합니다.
4.  **`ExperimentManager` 수정**: `CompleteCurrentTrial` 메서드 내에서 `FindObjectOfType<GoogleSpreadsheetsManager>().EnqueueTrial(rec)`을 호출하도록 코드를 수정해야 합니다.

## 📊 CSV 데이터 포맷

실험 종료 시 `DataLogger.cs`에 의해 생성되는 `.csv` 파일의 헤더는 다음과 같습니다.

```csv
Round,ID,Amplitude(A),Width(W),MovementTime(MT),ErrorRate(ER),Throughput(TP),TouchPosX,TouchPosY,TouchPosZ,TargetCenterX,TargetCenterY,TargetCenterZ,TouchOffset,HeadMovement
