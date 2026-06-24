-- =====================================================================
-- Sửa lệch múi giờ cho các bản ghi CHẤM CÔNG NHẬP TAY cũ (lệch +7h).
-- =====================================================================
-- Bối cảnh: form nhập tay trước đây gửi giờ local dạng "2026-06-23T06:00"
-- không kèm timezone; backend coi đó là UTC nên lưu lệch +7h so với giờ VN
-- người dùng nhập. Bản ghi chấm công TỰ ĐỘNG (check-in/kiosk) vẫn ĐÚNG.
--
-- ⚠️ KHÔNG có cột phân biệt bản ghi nhập tay vs tự động. PHẢI tự chọn đúng
--    bản ghi cần sửa — KHÔNG dịch lùi 7h toàn bộ bảng (sẽ hỏng dữ liệu đúng).
--
-- Cách dùng:
--   1) Chạy BƯỚC 1 để xem trước: cột check_in_local là giờ ĐANG hiển thị,
--      cột check_in_fixed_local là giờ SAU KHI sửa (-7h).
--   2) Xác định Id các bản ghi nhập tay sai → điền vào danh sách ở BƯỚC 2.
--   3) Chạy BƯỚC 2 trong transaction, kiểm tra rồi COMMIT (hoặc ROLLBACK).
-- =====================================================================

-- ---------------------------------------------------------------------
-- BƯỚC 1 — XEM TRƯỚC (chỉ đọc). Lọc theo tháng/NV để dễ rà.
-- ---------------------------------------------------------------------
SELECT
    r."Id",
    r."EmployeeId",
    e."FullName",
    e."EmployeeCode",
    r."WorkDate",
    -- Giờ đang hiển thị trên bảng (quy đổi UTC -> giờ VN):
    (r."CheckInTime"  AT TIME ZONE 'Asia/Ho_Chi_Minh') AS check_in_local,
    (r."CheckOutTime" AT TIME ZONE 'Asia/Ho_Chi_Minh') AS check_out_local,
    -- Giờ SAU KHI sửa (-7h) — đây là giờ người nhập tay thực sự muốn:
    ((r."CheckInTime"  - INTERVAL '7 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh') AS check_in_fixed_local,
    ((r."CheckOutTime" - INTERVAL '7 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh') AS check_out_fixed_local,
    r."Note"
FROM "AttendanceRecords" r
LEFT JOIN "EmployeeReferences" e ON e."EmployeeId" = r."EmployeeId"
WHERE r."CheckInTime" IS NOT NULL
  -- TÙY CHỌN: thu hẹp phạm vi rà soát
  -- AND r."WorkDate" BETWEEN DATE '2026-06-01' AND DATE '2026-06-30'
  -- AND e."EmployeeCode" = '9005'
ORDER BY r."WorkDate", e."EmployeeCode";

-- ---------------------------------------------------------------------
-- BƯỚC 2 — SỬA (dịch lùi 7h cho ĐÚNG các bản ghi đã chọn ở Bước 1).
--          Thay danh sách Id bên dưới bằng Id thực tế. Chạy trong transaction.
-- ---------------------------------------------------------------------
-- BEGIN;
--
-- UPDATE "AttendanceRecords"
-- SET "CheckInTime"  = "CheckInTime"  - INTERVAL '7 hours',
--     "CheckOutTime" = CASE WHEN "CheckOutTime" IS NOT NULL
--                           THEN "CheckOutTime" - INTERVAL '7 hours' END
-- WHERE "Id" IN (
--     '00000000-0000-0000-0000-000000000000'   -- ⬅ điền Id từ Bước 1
--     -- , '...'
-- );
--
-- -- Kiểm tra lại bằng BƯỚC 1 trước khi quyết định:
-- COMMIT;   -- hoặc ROLLBACK; nếu thấy sai
-- =====================================================================
-- Ghi chú:
--  * "WorkedHours"/"OvertimeHours" KHÔNG cần sửa (là hiệu số, dịch đều 2 đầu).
--  * "LateMinutes"/"EarlyLeaveMinutes" của bản ghi nhập tay cũ vốn = 0 (code cũ
--    không tính). Muốn tính lại cho chuẩn: mở bản ghi trên UI, bấm "Sửa" rồi
--    "Lưu" — backend mới sẽ tự tính ca + phút đi muộn.
-- =====================================================================
