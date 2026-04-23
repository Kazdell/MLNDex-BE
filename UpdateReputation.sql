-- 1. Xóa bảng TrustScoreHistories cũ
IF OBJECT_ID('dbo.TrustScoreHistories', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.TrustScoreHistories;
END
GO

-- 2. Xoá cột TrustScore trong bảng User
IF COL_LENGTH('dbo.User', 'TrustScore') IS NOT NULL
BEGIN
    -- Nếu có constraint (ví dụ DF constraint) liên kết với cột TrustScore thì cần drop constraint trước.
    -- Đoạn này giả sử không có default constraint được đặt tên cứng, hoặc bạn cần drop thủ công nếu có báo lỗi.
    ALTER TABLE dbo.[User] DROP COLUMN TrustScore;
END
GO

-- 3. Xoá cột TrustScore trong bảng TranslationTeam
IF COL_LENGTH('dbo.TranslationTeam', 'TrustScore') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TranslationTeam DROP COLUMN TrustScore;
END
GO

-- 4. Đặt giá trị mặc định ReputationScore = 100 cho những Creator/Team chưa có điểm
UPDATE dbo.CreatorProfile
SET ReputationScore = 100
WHERE ReputationScore = 0 OR ReputationScore IS NULL;
GO

UPDATE dbo.TranslationTeam
SET ReputationScore = 100
WHERE ReputationScore = 0 OR ReputationScore IS NULL;
GO

-- 5. Tạo bảng ReputationHistories mới
IF OBJECT_ID('dbo.ReputationHistories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReputationHistories (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CreatorId INT NULL,
        TranslationTeamId INT NULL,
        ScoreChange INT NOT NULL,
        Reason NVARCHAR(500) NOT NULL,
        RelatedReportId INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        
        CONSTRAINT FK_ReputationHistories_CreatorProfile_CreatorId FOREIGN KEY (CreatorId) REFERENCES dbo.CreatorProfile(CreatorId) ON DELETE CASCADE,
        CONSTRAINT FK_ReputationHistories_TranslationTeam_TranslationTeamId FOREIGN KEY (TranslationTeamId) REFERENCES dbo.TranslationTeam(TeamId) ON DELETE CASCADE,
        CONSTRAINT FK_ReputationHistories_Report_RelatedReportId FOREIGN KEY (RelatedReportId) REFERENCES dbo.Report(ReportId) ON DELETE SET NULL
    );
    
    CREATE INDEX IX_ReputationHistories_CreatorId ON dbo.ReputationHistories(CreatorId);
    CREATE INDEX IX_ReputationHistories_TranslationTeamId ON dbo.ReputationHistories(TranslationTeamId);
    CREATE INDEX IX_ReputationHistories_RelatedReportId ON dbo.ReputationHistories(RelatedReportId);
END
GO
