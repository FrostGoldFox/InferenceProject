# MainServer MySQL 스키마

## 적용 대상

| 항목 | 값 |
|---|---|
| Docker 컨테이너 | `inference-mysql` |
| 데이터베이스 | `inference_db` |
| MySQL 버전 | `8.4.11` |

아래 SQL은 `ProductName`, `Client`, `SuccessRate` 테이블을 생성할 때 실제로 사용한 구문이다.

> 현재 `inference_db`에는 세 테이블이 이미 생성되어 있다. 아래 SQL에는 `IF NOT EXISTS`가 없으므로 같은 데이터베이스에서 다시 실행하면 테이블이 이미 존재한다는 오류가 발생한다.

## 테이블 생성 SQL

```sql
CREATE TABLE `ProductName` (
  `ProductId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `ProductName` VARCHAR(100) NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`ProductId`)
) ENGINE=InnoDB
  DEFAULT CHARACTER SET=utf8mb4
  COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `Client` (
  `ClientId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `ClientName` VARCHAR(100) NOT NULL,
  `IPAddress` VARCHAR(45) NULL,
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`ClientId`)
) ENGINE=InnoDB
  DEFAULT CHARACTER SET=utf8mb4
  COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `SuccessRate` (
  `SuccessRateId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `ClientId` BIGINT UNSIGNED NOT NULL,
  `ProductId` BIGINT UNSIGNED NOT NULL,
  `SuccessCount` INT UNSIGNED NOT NULL,
  `FailureCount` INT UNSIGNED NOT NULL,
  `SuccessRate` DECIMAL(5,2) GENERATED ALWAYS AS (
    CASE
      WHEN (`SuccessCount` + `FailureCount`) = 0 THEN 0.00
      ELSE ROUND(
        (`SuccessCount` * 100.0) / (`SuccessCount` + `FailureCount`),
        2
      )
    END
  ) STORED,
  `CloudSyncedAt` DATETIME(6) NULL,
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`SuccessRateId`),
  INDEX `IXSuccessRateClientId` (`ClientId`),
  INDEX `IXSuccessRateProductId` (`ProductId`),
  INDEX `IXSuccessRateCreatedAt` (`CreatedAt`),
  CONSTRAINT `CKSuccessRateCount`
    CHECK ((`SuccessCount` + `FailureCount`) > 0),
  CONSTRAINT `FKSuccessRateClient`
    FOREIGN KEY (`ClientId`)
    REFERENCES `Client` (`ClientId`)
    ON UPDATE RESTRICT
    ON DELETE RESTRICT,
  CONSTRAINT `FKSuccessRateProduct`
    FOREIGN KEY (`ProductId`)
    REFERENCES `ProductName` (`ProductId`)
    ON UPDATE RESTRICT
    ON DELETE RESTRICT
) ENGINE=InnoDB
  DEFAULT CHARACTER SET=utf8mb4
  COLLATE=utf8mb4_0900_ai_ci;
```

## 주요 규칙

- `SuccessRate`는 직접 입력하지 않고 MySQL이 자동 계산한다.
- 계산식은 `SuccessCount / (SuccessCount + FailureCount) * 100`이다.
- `SuccessCount + FailureCount`는 반드시 0보다 커야 한다.
- `ClientId`와 `ProductId`는 존재하는 Client 및 제품을 참조해야 한다.
- 참조 중인 Client 또는 제품은 바로 삭제할 수 없도록 `RESTRICT`를 적용했다.
