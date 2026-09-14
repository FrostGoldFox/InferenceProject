# InferenceServer

FastAPI와 PyTorch 기반 이미지 추론 서버입니다. 모델은 프로세스 시작 시 한 번만 로드하고 모든 요청에서 재사용합니다.

## 설치 및 실행

```powershell
python -m venv .venv
.\.venv\Scripts\pip.exe install -r requirements.txt
$env:INFERENCE_CHECKPOINT_PATH = "C:\path\to\checkpoint.pt"
$env:MIDDLEWARE_RESPONSE_URL = "http://MIDDLEWARE_HOST:5073/ResponSE"
.\.venv\Scripts\python.exe main.py
```

서버는 기본적으로 `0.0.0.0:8000`에서 수신합니다.

## 환경변수

- `INFERENCE_CHECKPOINT_PATH`: 모델 체크포인트. 미설정 시 `InferenceServer/checkpoint.pt`
- `INFERENCE_DEVICE`: 기본 `auto`
- `INFERENCE_SCORE_THRESHOLD`: 기본 `0.4`
- `MIDDLEWARE_RESPONSE_URL`: 기본 `http://localhost:5073/ResponSE`

## API

- `GET /health`: 모델 로드, 큐, 처리·실패 횟수 및 마지막 오류 상태
- `POST /message`: Base64 이미지 JSON 접수, 정상 시 `202 Accepted`

```json
{
  "type": "request",
  "client": 1,
  "filename": "image.jpg",
  "filelength": 1024,
  "filedata": "BASE64_IMAGE"
}
```

최대 디코딩 이미지 크기는 20 MiB, 최대 픽셀 수는 25,000,000, 작업 큐 용량은 32건입니다. 큐가 가득 차면 `503`, 크기 초과는 `413`, 잘못된 요청은 `400`을 반환합니다.

## 결과

가장 높은 신뢰도 검출의 제품명, 신뢰도, Box를 MiddleWare `POST /ResponSE`로 전송합니다. 임계값 이상 검출이 있으면 `sucess`, 없으면 `fail`입니다. 전송 실패 시 최대 3회 재시도합니다.

체크포인트(`*.pt`, `*.pth`)와 로컬 환경은 Git에서 제외됩니다.
