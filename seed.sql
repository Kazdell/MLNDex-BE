-- Xoá dữ liệu rating hiện tại của user admin cho 2 series này (phòng hờ đã có)
DELETE FROM Rating WHERE UserId = (SELECT TOP 1 UserId FROM [User] WHERE Username = 'admin') AND SeriesId IN (8, 25);
DELETE FROM Follow WHERE UserId = (SELECT TOP 1 UserId FROM [User] WHERE Username = 'admin') AND TargetId IN (8, 25) AND TargetType = 'Series';

DECLARE @AdminUserId INT;
SELECT TOP 1 @AdminUserId = UserId FROM [User] WHERE Username = 'admin';

IF @AdminUserId IS NOT NULL
BEGIN
    -- Thêm dữ liệu Rating cho Series 8
    INSERT INTO Rating (UserId, SeriesId, Score, Review, CreatedAt, UpdatedAt)
    VALUES (@AdminUserId, 8, 9, N'Truyện rất hay, đáng xem!', GETUTCDATE(), GETUTCDATE());

    -- Thêm dữ liệu Rating cho Series 25 (không thêm để test hiển thị "Chưa có đánh giá")
    -- INSERT INTO Rating (UserId, SeriesId, Score, Review, CreatedAt, UpdatedAt)
    -- VALUES (@AdminUserId, 25, 8, N'Tạm được.', GETUTCDATE(), GETUTCDATE());

    -- Thêm dữ liệu Follow cho Series 8 và 25
    INSERT INTO Follow (UserId, TargetId, TargetType, FollowedAt)
    VALUES (@AdminUserId, 8, 'Series', GETUTCDATE());

    INSERT INTO Follow (UserId, TargetId, TargetType, FollowedAt)
    VALUES (@AdminUserId, 25, 'Series', GETUTCDATE());
    
    PRINT 'Đã seed dữ liệu Rating và Follow thành công.';
END
ELSE
BEGIN
    PRINT 'Không tìm thấy User admin';
END
GO

-- Cập nhật lại TotalRatings và AverageRating cho toàn bộ table Series
UPDATE S
SET 
    S.TotalRatings = ISNULL(R.TotalCount, 0),
    S.AverageRating = ISNULL(R.AvgScore, 0)
FROM Series S
LEFT JOIN (
    SELECT 
        SeriesId, 
        COUNT(RatingId) AS TotalCount, 
        CAST(AVG(CAST(Score AS DECIMAL(10,2))) AS DECIMAL(10,2)) AS AvgScore
    FROM Rating
    GROUP BY SeriesId
) R ON S.SeriesId = R.SeriesId;

PRINT 'Hoàn thành cập nhật điểm đánh giá trung bình.';
GO
