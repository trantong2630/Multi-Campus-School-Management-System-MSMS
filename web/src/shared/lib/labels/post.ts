// Phạm vi bài đăng bảng tin (BR-27). Khóa là Post.Visibility lưu trong DB.
export const postVisibilityLabel: Record<string, string> = {
  Internal: 'Nội bộ',
  School: 'Toàn trường',
};

export const postVisibilityHint: Record<string, string> = {
  Internal: 'Chỉ cán bộ đọc được.',
  School: 'Mọi người đọc được, gồm học sinh và phụ huynh.',
};

export const MAX_POST_IMAGES = 5;
export const MAX_POST_IMAGE_BYTES = 2 * 1024 * 1024;
