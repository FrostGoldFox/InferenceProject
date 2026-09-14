namespace inferenceclinet.Models;

// 화면 표시용 모델. MainServer의 MainResponseMessage(ProductName/SucessRate/Box)를 그대로 담는다.
// DefectType = 실제로는 공압 부품 형번(예: KCPC형). InferenceServer는 4단계 체결상태(미체결/부분체결/오류체결/완전체결)도
// 계산하지만 현재 서버 계약은 이진 성공/실패만 전달함 — 서버 쪽 계약이 확장되면 이 모델도 같이 확장 필요.
// Box = "x1, y1, x2, y2" 형태의 표시용 문자열 (INFERENCE_BOX_API_CHANGE.md). 미검출 시 "-".
// 실제 화면에 사각형을 그리려면 원본 이미지 크기 대비 배율 계산이 추가로 필요 (아직 미구현).
public record InspectionRecord(DateTime Time, string Result, string? Confidence, string? DefectType, string? Box = null);
