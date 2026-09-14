# 프로젝트 계획 (팀 공통)

## 주제

카메라로 촬영한 이미지를 Inference Server로 보내 조립 여부 / 조립된 부품 이름을 판별한다.

## 구간별 담당 (Data Stream)

| # | 구간 | 비고 |
|---|---|---|
| ① | Client ↔ Middleware ↔ MainServer Login Module | **내 담당** — [01-client-modules.md](01-client-modules.md) |
| ② | Client ↔ Middleware ↔ MainServer Response Module | **내 담당** — [01-client-modules.md](01-client-modules.md) |
| ③ | MainServer ↔ Middleware ↔ Inference Server | |
| ④ | MainServer ↔ DB | |
| ⑤ | DB ↔ Admin Client | |

## 일정

- **09/09 ~ 09/11**: 각자 담당 구간 제작
- **Middleware**: Client / MainServer / Inference Server 제작이 모두 끝난 뒤 마지막에 제작 (그 전까지는 각 구간 담당자가 HTTP 라우트/스펙만 MD로 합의)
- HTTP Router는 **Client Router**와 **Middleware Router**를 구분해서 각자 제작

## 작업 방식

1. 코드 작성 전, 각자 담당 구간의 제작 계획을 MD 파일로 먼저 작성
2. 작성한 MD 파일을 기준으로 팀에 공유 (구현은 공유된 MD 스펙을 따름)
3. 전원 MD 파일 작성 완료 후, 모아서 아래 산출물 제작
   - 요구사항 분석서
   - 테이블 명세서
   - ERD
   - API 명세서

## 관련 문서

- [01-client-modules.md](01-client-modules.md) — Client 담당 (Login Module, Response Module)
