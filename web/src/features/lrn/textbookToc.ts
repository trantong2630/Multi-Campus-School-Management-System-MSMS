/**
 * Mục lục bộ "Kết nối tri thức với cuộc sống" (chỉ tên bài, không chép nội dung sách) để gợi ý ô Chủ đề của quầy soạn MSMS AI.
 * Khóa ngoài là tên môn trong danh mục, khóa trong là khối. Môn hoặc khối chưa có ở đây thì ô Chủ đề vẫn gõ tay như thường.
 */
export const TEXTBOOK_TOC: Record<string, Record<number, string[]>> = {
  'Toán': {
    6: [
      'Bài 1. Tập hợp',
      'Bài 2. Cách ghi số tự nhiên',
      'Bài 3. Thứ tự trong tập hợp các số tự nhiên',
      'Bài 4. Phép cộng và phép trừ số tự nhiên',
      'Bài 5. Phép nhân và phép chia số tự nhiên',
      'Bài 6. Lũy thừa với số mũ tự nhiên',
      'Bài 7. Thứ tự thực hiện các phép tính',
      'Bài 8. Quan hệ chia hết và tính chất',
      'Bài 9. Dấu hiệu chia hết',
      'Bài 10. Số nguyên tố',
      'Bài 11. Ước chung. Ước chung lớn nhất',
      'Bài 12. Bội chung. Bội chung nhỏ nhất',
      'Bài 13. Tập hợp các số nguyên',
      'Bài 14. Phép cộng và phép trừ số nguyên',
      'Bài 15. Quy tắc dấu ngoặc',
      'Bài 16. Phép nhân số nguyên',
      'Bài 17. Phép chia hết. Ước và bội của một số nguyên',
      'Bài 18. Hình tam giác đều. Hình vuông. Hình lục giác đều',
      'Bài 19. Hình chữ nhật. Hình thoi. Hình bình hành. Hình thang cân',
      'Bài 20. Chu vi và diện tích của một số tứ giác đã học',
      'Bài 21. Hình có trục đối xứng',
      'Bài 22. Hình có tâm đối xứng',
      'Bài 23. Mở rộng phân số. Phân số bằng nhau',
      'Bài 24. So sánh phân số. Hỗn số dương',
      'Bài 25. Phép cộng và phép trừ phân số',
      'Bài 26. Phép nhân và phép chia phân số',
      'Bài 27. Hai bài toán về phân số',
      'Bài 28. Số thập phân',
      'Bài 29. Tính toán với số thập phân',
      'Bài 30. Làm tròn và ước lượng',
      'Bài 31. Một số bài toán về tỉ số và tỉ số phần trăm',
      'Bài 32. Điểm và đường thẳng',
      'Bài 33. Điểm nằm giữa hai điểm. Tia',
      'Bài 34. Đoạn thẳng. Độ dài đoạn thẳng',
      'Bài 35. Trung điểm của đoạn thẳng',
      'Bài 36. Góc',
      'Bài 37. Số đo góc',
      'Bài 38. Dữ liệu và thu thập dữ liệu',
      'Bài 39. Bảng thống kê và biểu đồ tranh',
      'Bài 40. Biểu đồ cột',
      'Bài 41. Biểu đồ cột kép',
      'Bài 42. Kết quả có thể và sự kiện trong trò chơi, thí nghiệm',
      'Bài 43. Xác suất thực nghiệm',
    ],
  },
};

export function topicSuggestions(subjectName: string | undefined, grade: number): string[] {
  return (subjectName && TEXTBOOK_TOC[subjectName]?.[grade]) || [];
}
