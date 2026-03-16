-- Fix team 20
UPDATE TranslationTeam SET LanguageId = 1 WHERE TeamId = 20;
GO

-- Xác nhận hết NULL
SELECT TeamId, LanguageId FROM TranslationTeam WHERE LanguageId IS NULL;
GO

ALTER TABLE TranslationTeam ALTER COLUMN LanguageId INT NOT NULL;
ALTER TABLE TranslationTeam ADD CONSTRAINT FK_TranslationTeam_Language 
    FOREIGN KEY (LanguageId) REFERENCES [Language](LanguageId);
ALTER TABLE TranslationTeam DROP COLUMN Language;
GO

-- Chapter: thêm LanguageId FK (nullable)
ALTER TABLE Chapter ADD LanguageId INT NULL;
GO
ALTER TABLE Chapter ADD CONSTRAINT FK_Chapter_Language 
    FOREIGN KEY (LanguageId) REFERENCES [Language](LanguageId);
GO

-- Translation: thêm LanguageId FK
ALTER TABLE Translation ADD LanguageId INT NULL;
GO

UPDATE Translation SET LanguageId = COALESCE(
    (SELECT TOP 1 LanguageId FROM [Language] WHERE Name = Translation.Language),
    (SELECT TOP 1 LanguageId FROM [Language] WHERE Code = 'vi')
);
GO

ALTER TABLE Translation ALTER COLUMN LanguageId INT NOT NULL;
ALTER TABLE Translation ADD CONSTRAINT FK_Translation_Language 
    FOREIGN KEY (LanguageId) REFERENCES [Language](LanguageId);
ALTER TABLE Translation DROP COLUMN Language;
GO
