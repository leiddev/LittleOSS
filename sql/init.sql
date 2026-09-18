-- =============================================================================
-- LittleOSS 数据库初始化脚本（MySQL 8.0+）
-- =============================================================================
-- 用法：
--   mysqlsh --sql --uri "mysql://root:密码@127.0.0.1:3306" -f sql/init.sql
--   mysql   -h 127.0.0.1 -P 3306 -u root -p < sql/init.sql
--
-- 说明：
--   1. 应用启动时会调用 Database.EnsureCreatedAsync() 自动建库建表，本脚本用于
--      手工初始化场景（DBA 审批、CI/CD、容器化部署、只给 DDL 不给应用权限等）。
--   2. EnsureCreatedAsync() 只在库中「一张表都没有」时才建表，不会对比或补齐已存在
--      的表结构。因此使用本脚本预建表是安全的（EF 会跳过），但也意味着表结构必须与
--      EF 模型（Data/OssDbContext.cs + Models/FileMetadata.cs）保持一致；模型变更后
--      需同步修改本文件，或改用 EF Migrations。
--   3. 脚本幂等，可重复执行（CREATE DATABASE/TABLE IF NOT EXISTS）。
--   4. 库名 littleoss 需与 appsettings.json 中 Oss:Database:ConnectionString 的
--      Database 参数一致。
--
-- 列类型映射（Pomelo.EntityFrameworkCore.MySql 默认约定，本文件按 EF 实际生成结果编写）：
--   Guid     -> char(36) CHARACTER SET ascii   （依赖连接字符串 GuidFormat，默认 Char36）
--   string   -> varchar(MaxLength)
--   long     -> bigint
--   bool     -> tinyint(1)
--   DateTime -> datetime(6)
--
-- 表名：EF 生成的是 `FileMetadatas`。Windows 下 MySQL 默认 lower_case_table_names=1，
--       实际会以小写 `filemetadatas` 存储；Linux 下则保留 `FileMetadatas`。
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `littleoss`
  DEFAULT CHARACTER SET utf8mb4
  DEFAULT COLLATE utf8mb4_0900_ai_ci;

USE `littleoss`;

-- -----------------------------------------------------------------------------
-- 文件元数据表（Models/FileMetadata.cs）
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `FileMetadatas` (
  `Id`               char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FileId`           varchar(64)  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `OriginalFileName` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Region`           varchar(64)  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `FilePath`         varchar(512) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `FileSizeBytes`    bigint       NOT NULL,
  `ContentType`      varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `UploadTime`       datetime(6)  NOT NULL,
  `AccessKeyId`      varchar(64)  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsDeleted`        tinyint(1)   NOT NULL,
  `DeletedAt`        datetime(6)  DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_FileMetadatas_FileId` (`FileId`),
  KEY `IX_FileMetadatas_AccessKeyId` (`AccessKeyId`),
  KEY `IX_FileMetadatas_Region` (`Region`),
  KEY `IX_FileMetadatas_Region_IsDeleted` (`Region`,`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
