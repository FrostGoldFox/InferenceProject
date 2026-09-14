# MySQL 테이블 명세

기본 데이터베이스 이름은 `inference_db`입니다. 실제 비밀번호는 SQL 또는 문서에 저장하지 않고 환경변수로 주입합니다.

## Login

| 컬럼 | 형식 | 제약 |
|---|---|---|
| UID | INT | PK, AUTO_INCREMENT |
| ID | VARCHAR(20) | UNIQUE, NOT NULL |
| HashPassword | VARCHAR(64) | NOT NULL |

## ProductName

| 컬럼 | 형식 | 제약 |
|---|---|---|
| ProductId | BIGINT UNSIGNED | PK, AUTO_INCREMENT |
| ProductName | VARCHAR(100) | NOT NULL |
| CreatedAt | DATETIME(6) | NOT NULL, 기본 현재 시각 |

## Client

| 컬럼 | 형식 | 제약 |
|---|---|---|
| ClientId | BIGINT UNSIGNED | PK, AUTO_INCREMENT |
| ClientName | VARCHAR(100) | NOT NULL |
| IPAddress | VARCHAR(45) | NULL |
| CreatedAt | DATETIME(6) | NOT NULL, 기본 현재 시각 |

## SuccessRate

| 컬럼 | 형식 | 제약 |
|---|---|---|
| SuccessRateId | BIGINT UNSIGNED | PK, AUTO_INCREMENT |
| ClientId | BIGINT UNSIGNED | FK Client |
| ProductId | BIGINT UNSIGNED | FK ProductName |
| SuccessCount | INT UNSIGNED | NOT NULL |
| FailureCount | INT UNSIGNED | NOT NULL |
| SuccessRate | DECIMAL(5,2) | 성공/전체 비율 |
| CloudSyncedAt | DATETIME(6) | NULL |
| CreatedAt | DATETIME(6) | NOT NULL, 기본 현재 시각 |

```sql
CREATE TABLE `Login` (
  `UID` INT NOT NULL AUTO_INCREMENT,
  `ID` VARCHAR(20) NOT NULL,
  `HashPassword` VARCHAR(64) NOT NULL,
  PRIMARY KEY (`UID`),
  UNIQUE KEY `UXLoginID` (`ID`)
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `ProductName` (
  `ProductId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `ProductName` VARCHAR(100) NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `Client` (
  `ClientId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `ClientName` VARCHAR(100) NOT NULL,
  `IPAddress` VARCHAR(45) NULL,
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`ClientId`)
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `SuccessRate` (
  `SuccessRateId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `ClientId` BIGINT UNSIGNED NOT NULL,
  `ProductId` BIGINT UNSIGNED NOT NULL,
  `SuccessCount` INT UNSIGNED NOT NULL,
  `FailureCount` INT UNSIGNED NOT NULL,
  `SuccessRate` DECIMAL(5,2) GENERATED ALWAYS AS (
    CASE
      WHEN (`SuccessCount` + `FailureCount`) = 0 THEN 0.00
      ELSE ROUND((`SuccessCount` * 100.0) / (`SuccessCount` + `FailureCount`), 2)
    END
  ) STORED,
  `CloudSyncedAt` DATETIME(6) NULL,
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`SuccessRateId`),
  INDEX `IXSuccessRateClientId` (`ClientId`),
  INDEX `IXSuccessRateProductId` (`ProductId`),
  INDEX `IXSuccessRateCreatedAt` (`CreatedAt`),
  CONSTRAINT `FKSuccessRateClient` FOREIGN KEY (`ClientId`) REFERENCES `Client` (`ClientId`),
  CONSTRAINT `FKSuccessRateProduct` FOREIGN KEY (`ProductId`) REFERENCES `ProductName` (`ProductId`),
  CONSTRAINT `CKSuccessRateCount` CHECK ((`SuccessCount` + `FailureCount`) > 0)
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;
```

위 SQL은 초기 생성 기준입니다. 최근 100건 누적 성공률을 행에 저장하는 운영 DB로 마이그레이션했다면 `SuccessRate` 일반 컬럼 및 갱신 절차를 해당 배포 스키마에 맞춰 유지해야 합니다.
