-- =============================================================================
-- LittleOSS 数据库初始化脚本（SQLite）
-- =============================================================================
-- 用法（在项目根目录执行）：
--   sqlite3 oss.db ".read sql/init.sqlite.sql"      # 推荐，PowerShell / cmd 通用
--
--   也可用输入重定向（注意：PowerShell 不支持 `<`，需在 cmd 或 bash 下执行）：
--   sqlite3 oss.db < sql/init.sqlite.sql
--
--   没有 sqlite3 CLI 时用 Python：
--   python -c "import sqlite3;sqlite3.connect('oss.db').executescript(open('sql/init.sqlite.sql',encoding='utf-8').read())"
--
-- 说明：
--   1. 应用启动时会调用 Database.EnsureCreatedAsync() 自动建库建表，本脚本用于手工
--      初始化场景（预置数据库文件、CI/CD、单独审阅 DDL 等）。
--   2. SQLite 是「一个文件一个库」，没有独立的建库语句：目标文件不存在时会由
--      sqlite3 自动创建。文件路径需与 appsettings.json 中 Oss:Database:SqlitePath
--      一致（默认 `oss.db`，相对内容根目录解析；给绝对路径也可）。
--   3. EnsureCreatedAsync() 只在库中「一张表都没有」时才建表，不会比对或补齐已存在的
--      表结构。因此本脚本必须与 EF 模型（Data/OssDbContext.cs + Models/FileMetadata.cs）
--      保持一致；模型变更后需同步修改本文件，或改用 EF Migrations。
--   4. 脚本幂等，可重复执行（CREATE ... IF NOT EXISTS）。
--
-- 类型映射（Microsoft.EntityFrameworkCore.Sqlite 默认约定，本文件按 EF 实际生成结果编写）：
--   Guid     -> TEXT，大写字符串形式（如 A1B46D98-417C-432B-B775-7E6883B356E7）
--   string   -> TEXT
--   long     -> INTEGER
--   bool     -> INTEGER（0/1）
--   DateTime -> TEXT（EF 的 SQLite 日期时间格式）
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 文件元数据表（Models/FileMetadata.cs）
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "FileMetadatas" (
    "Id"               TEXT    NOT NULL CONSTRAINT "PK_FileMetadatas" PRIMARY KEY,
    "FileId"           TEXT    NOT NULL,
    "OriginalFileName" TEXT    NOT NULL,
    "Region"           TEXT    NOT NULL,
    "FilePath"         TEXT    NOT NULL,
    "FileSizeBytes"    INTEGER NOT NULL,
    "ContentType"      TEXT    NOT NULL,
    "UploadTime"       TEXT    NOT NULL,
    "AccessKeyId"      TEXT    NOT NULL,
    "IsDeleted"        INTEGER NOT NULL,
    "DeletedAt"        TEXT    NULL
);

-- -----------------------------------------------------------------------------
-- 索引（名称与 EF 生成的保持一致）
-- -----------------------------------------------------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS "IX_FileMetadatas_FileId"            ON "FileMetadatas" ("FileId");
CREATE INDEX        IF NOT EXISTS "IX_FileMetadatas_AccessKeyId"       ON "FileMetadatas" ("AccessKeyId");
CREATE INDEX        IF NOT EXISTS "IX_FileMetadatas_Region"            ON "FileMetadatas" ("Region");
CREATE INDEX        IF NOT EXISTS "IX_FileMetadatas_Region_IsDeleted"  ON "FileMetadatas" ("Region", "IsDeleted");
