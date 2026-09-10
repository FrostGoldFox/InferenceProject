# AdminClient

솔루션에 포함된 .NET 10 WPF 프로젝트다.

## 현재 상태

현재 `MainWindow`가 빈 기본 스켈레톤이므로 로그인, 관리 기록, 통계, DB 조회가 구현되어 있지 않다.

실제 UI와 MySQL 조회 코드는 별도 프로젝트 `C:\Users\user\AdminClientWpf`에 있다. 두 프로젝트를 혼동하지 않도록 향후 별도 프로젝트를 이 위치로 이전하거나 솔루션에 직접 추가해야 한다.

AdminClient의 DB 접근은 HTTP가 아니라 MySqlConnector를 이용한 직접 MySQL 조회 방식으로 구현되어 있다. 조회 전용 DB 계정 사용이 필요하다.

