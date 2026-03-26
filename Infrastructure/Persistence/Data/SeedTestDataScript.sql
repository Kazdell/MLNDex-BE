-- MLNDex Seed Test Data Script
-- Dữ liệu: Roles & Users (Admin + 10 Test Users)

-- 1. Insert Roles
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'READER') INSERT INTO [Role] (RoleName) VALUES ('READER');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'CREATOR') INSERT INTO [Role] (RoleName) VALUES ('CREATOR');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'TRANSLATOR') INSERT INTO [Role] (RoleName) VALUES ('TRANSLATOR');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'MODERATOR') INSERT INTO [Role] (RoleName) VALUES ('MODERATOR');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleName = 'ADMIN') INSERT INTO [Role] (RoleName) VALUES ('ADMIN');

-- 2. Insert Users
-- Admin (Password: Admin@123)
-- Users (Password: Password123)
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Username = 'admin')
BEGIN
    INSERT INTO [User] (Username, Email, DisplayName, PasswordHash, IsEmailVerified, IsActive, CreatedAt, TrustScore, CannotUpload)
    VALUES ('admin', 'admin@gmail.com', 'System Admin', '$2a$11$FOXg0bgMDFmtMMqdRtzfWeHc1W03SxgclAdXCO3mtSi4wrWVHJQLa', 1, 1, GETUTCDATE(), 100, 0);
END

IF NOT EXISTS (SELECT 1 FROM [User] WHERE Username = 'leader_team1')
BEGIN
    INSERT INTO [User] (Username, Email, DisplayName, PasswordHash, IsEmailVerified, IsActive, CreatedAt, TrustScore, CannotUpload)
    VALUES 
    ('leader_team1', 'leader1@test.com', 'Leader Team 1', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('leader_team2', 'leader2@test.com', 'Leader Team 2', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('member1_team1', 'member1.1@test.com', 'Member 1.1', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('member2_team1', 'member1.2@test.com', 'Member 1.2', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('member3_team1', 'member1.3@test.com', 'Member 1.3', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('member4_team1', 'member1.4@test.com', 'Member 1.4', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('member1_team2', 'member2.1@test.com', 'Member 2.1', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('member2_team2', 'member2.2@test.com', 'Member 2.2', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('member3_team2', 'member2.3@test.com', 'Member 2.3', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0),
    ('member4_team2', 'member2.4@test.com', 'Member 2.4', '$2a$11$mC7p.hpzZ.37XvAWhsL7Te9e3C5M.l.pI1E.r/X0UfO0U.v.Uv.m', 1, 1, GETUTCDATE(), 0, 0);
END

-- 3. Assign Roles
-- Admin Role
INSERT INTO [UserRole] (UserId, RoleId, AssignedAt)
SELECT u.UserId, r.RoleId, GETUTCDATE()
FROM [User] u, [Role] r
WHERE u.Username = 'admin' AND r.RoleName = 'ADMIN'
AND NOT EXISTS (SELECT 1 FROM [UserRole] ur WHERE ur.UserId = u.UserId AND ur.RoleId = r.RoleId);

-- Translator Role for others
INSERT INTO [UserRole] (UserId, RoleId, AssignedAt)
SELECT u.UserId, r.RoleId, GETUTCDATE()
FROM [User] u, [Role] r
WHERE u.Username IN ('leader_team1', 'leader_team2', 'member1_team1', 'member2_team1', 'member3_team1', 'member4_team1', 'member1_team2', 'member2_team2', 'member3_team2', 'member4_team2')
AND r.RoleName = 'TRANSLATOR'
AND NOT EXISTS (SELECT 1 FROM [UserRole] ur WHERE ur.UserId = u.UserId AND ur.RoleId = r.RoleId);
