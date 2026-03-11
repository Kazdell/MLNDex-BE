-- MLNDex Seed Test Data Script
-- Dữ liệu: 10 Users, 2 Teams (Verified), Leader & Membership

-- 1. Insert Roles (nếu chưa có)
-- Lưu ý: RoleName lưu dưới dạng String theo cấu hình EF Core
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'READER') INSERT INTO [Role] (RoleName) VALUES ('READER');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'CREATOR') INSERT INTO [Role] (RoleName) VALUES ('CREATOR');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'TRANSLATOR') INSERT INTO [Role] (RoleName) VALUES ('TRANSLATOR');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'MODERATOR') INSERT INTO [Role] (RoleName) VALUES ('MODERATOR');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'ADMIN') INSERT INTO [Role] (RoleName) VALUES ('ADMIN');

-- 2. Insert Users (Password: Password123)
-- Hash: $2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m
-- Tạo 10 Users
INSERT INTO [User] (Username, Email, DisplayName, PasswordHash, IsEmailVerified, IsActive, CreatedAt)
VALUES 
('leader_team1', 'leader1@test.com', 'Leader Team 1', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('leader_team2', 'leader2@test.com', 'Leader Team 2', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('member1_team1', 'member1.1@test.com', 'Member 1.1', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('member2_team1', 'member1.2@test.com', 'Member 1.2', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('member3_team1', 'member1.3@test.com', 'Member 1.3', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('member4_team1', 'member1.4@test.com', 'Member 1.4', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('member1_team2', 'member2.1@test.com', 'Member 2.1', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('member2_team2', 'member2.2@test.com', 'Member 2.2', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('member3_team2', 'member2.3@test.com', 'Member 2.3', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE()),
('member4_team2', 'member2.4@test.com', 'Member 2.4', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE());

-- 3. Gán Role TRANSLATOR cho tất cả Users mới
INSERT INTO [UserRole] (UserId, RoleId, AssignedAt)
SELECT u.UserId, r.RoleId, GETUTCDATE()
FROM [User] u, [Role] r
WHERE u.Username IN ('leader_team1', 'leader_team2', 'member1_team1', 'member2_team1', 'member3_team1', 'member4_team1', 'member1_team2', 'member2_team2', 'member3_team2', 'member4_team2')
AND r.RoleName = 'TRANSLATOR';

-- 4. Tạo 2 Translation Teams (LockStatus: ACTIVE, ModerationStatus: APPROVED)
INSERT INTO [TranslationTeam] (TeamName, Description, ReputationScore, LockStatus, ModerationStatus, CreatedAt, LeaderId, IsMonetizationEnabled)
VALUES 
(N'Hội Những Người Yêu Truyện', N'Nhóm dịch đam mê các thể loại phiêu lưu, kỳ ảo.', 100, 'ACTIVE', 'APPROVED', GETUTCDATE(), (SELECT UserId FROM [User] WHERE Username = 'leader_team1'), 1),
(N'Tiệm Dịch Cầu Vồng', N'Chuyên dịch các bộ truyện chữa lành và lãng mạn.', 100, 'ACTIVE', 'APPROVED', GETUTCDATE(), (SELECT UserId FROM [User] WHERE Username = 'leader_team2'), 1);

-- 5. Thêm Member vào Nhóm
-- Team 1
DECLARE @Team1Id INT = (SELECT TeamId FROM [TranslationTeam] WHERE TeamName = N'Hội Những Người Yêu Truyện');
INSERT INTO [TeamMember] (TeamId, UserId, Role, JoinedAt, IsActive)
VALUES 
(@Team1Id, (SELECT UserId FROM [User] WHERE Username = 'leader_team1'), 'LEADER', GETUTCDATE(), 1),
(@Team1Id, (SELECT UserId FROM [User] WHERE Username = 'member1_team1'), 'MEMBER', GETUTCDATE(), 1),
(@Team1Id, (SELECT UserId FROM [User] WHERE Username = 'member2_team1'), 'MEMBER', GETUTCDATE(), 1),
(@Team1Id, (SELECT UserId FROM [User] WHERE Username = 'member3_team1'), 'MEMBER', GETUTCDATE(), 1),
(@Team1Id, (SELECT UserId FROM [User] WHERE Username = 'member4_team1'), 'MEMBER', GETUTCDATE(), 1);

-- Team 2
DECLARE @Team2Id INT = (SELECT TeamId FROM [TranslationTeam] WHERE TeamName = N'Tiệm Dịch Cầu Vồng');
INSERT INTO [TeamMember] (TeamId, UserId, Role, JoinedAt, IsActive)
VALUES 
(@Team2Id, (SELECT UserId FROM [User] WHERE Username = 'leader_team2'), 'LEADER', GETUTCDATE(), 1),
(@Team2Id, (SELECT UserId FROM [User] WHERE Username = 'member1_team2'), 'MEMBER', GETUTCDATE(), 1),
(@Team2Id, (SELECT UserId FROM [User] WHERE Username = 'member2_team2'), 'MEMBER', GETUTCDATE(), 1),
(@Team2Id, (SELECT UserId FROM [User] WHERE Username = 'member3_team2'), 'MEMBER', GETUTCDATE(), 1),
(@Team2Id, (SELECT UserId FROM [User] WHERE Username = 'member4_team2'), 'MEMBER', GETUTCDATE(), 1);
