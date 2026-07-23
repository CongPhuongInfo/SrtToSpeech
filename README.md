# SrtToSpeechApp

Ứng dụng WinForms (VB.NET, .NET 9) chuyển file phụ đề `.srt` thành file âm thanh `.wav`, đọc từng câu bằng giọng TTS có sẵn trên Windows (SAPI) và **đặt đúng vị trí thời gian** theo mốc timestamp trong file SRT gốc — giống như lồng tiếng tự động theo phụ đề.

## Tính năng

- Đọc file `.srt` chuẩn (số thứ tự, mốc thời gian `-->`, nội dung), tự bỏ qua các thẻ định dạng như `<i>`, `<b>`, `{\an8}`...
- Liệt kê toàn bộ giọng đọc (voice) đã cài trên Windows, cho chọn giọng và làm mới danh sách
- Chỉnh tốc độ đọc (Rate: -10 đến 10)
- Tổng hợp giọng nói cho từng câu rồi **trộn vào đúng mốc thời gian** trong một file âm thanh duy nhất (không phải nối liền nhau)
- Thanh tiến trình theo % số câu đã xử lý, log chi tiết từng câu kèm giờ:phút:giây
- Cảnh báo khi một câu đọc quá dài, vượt khung thời gian của phụ đề đó (có thể đè lên câu kế tiếp)
- Xuất kết quả ra file `.wav`

## Yêu cầu hệ thống

- Windows
- [.NET 9 SDK](https://dotnet.microsoft.com/download) trở lên
- Ít nhất 1 giọng đọc (voice) TTS đã cài trên Windows

### Cài giọng đọc tiếng Việt (nếu cần)

Windows mặc định thường chỉ có giọng tiếng Anh. Để có giọng tiếng Việt:

1. Mở **Settings** → **Time & Language** → **Speech**
2. Ở mục **Manage voices**, bấm **Add voices**
3. Tìm và cài **Vietnamese (Vietnam)**
4. Mở lại app, bấm **"Làm mới danh sách"** để thấy giọng vừa cài

Nếu không cài giọng tiếng Việt, app chỉ đọc được bằng giọng đã có (thường là tiếng Anh) — nghĩa là văn bản tiếng Việt sẽ bị đọc sai giọng/âm.

## Cấu trúc project

```
SrtToSpeechApp/
├── Program.vb              Entry point (khởi động WinForms)
├── Form1.vb                 Giao diện chính (code thuần, không dùng designer)
├── SrtEngine.vb              Parse SRT + TTS (SAPI) + trộn audio theo timeline
├── SrtToSpeechApp.vbproj     File project (.NET 9, WinForms)
├── build.bat                 Script build (Release)
└── run.bat                   Script chạy nhanh sau khi build
```

## Cách build & chạy

1. Cài [.NET 9 SDK](https://dotnet.microsoft.com/download) nếu chưa có.
2. Chạy `build.bat` để restore NuGet packages và build bản Release.
3. Chạy `run.bat`, hoặc mở trực tiếp:
   ```
   bin\Release\net9.0-windows\SrtToSpeechApp.exe
   ```

## Cách dùng

1. Bấm **"Chọn file..."** để chọn file `.srt`.
2. Chọn giọng đọc ở ô **"Giọng đọc"** (bấm "Làm mới danh sách" nếu vừa cài thêm giọng).
3. Chỉnh **"Tốc độ đọc"** nếu muốn đọc nhanh/chậm hơn mặc định.
4. Bấm **"Chuyển đổi"**, theo dõi tiến trình và log chi tiết.
5. Sau khi hoàn tất, bấm **"Lưu file âm thanh..."** để xuất ra `.wav`.

## Cách hoạt động (kỹ thuật)

- Mỗi câu phụ đề được tổng hợp giọng nói riêng bằng `System.Speech.Synthesis`, định dạng cố định 22050Hz / 16-bit / mono để dễ trộn.
- Toàn bộ file âm thanh cuối cùng được tạo thành một mảng mẫu (samples) có độ dài bằng tổng thời lượng phụ đề (cộng thêm 1 giây đệm), ban đầu toàn bộ là im lặng.
- Mỗi câu được **cộng dồn (mix)** vào đúng vị trí mẫu tương ứng với mốc thời gian bắt đầu của câu đó trong SRT.
- Nếu giọng đọc mất nhiều thời gian hơn khung phụ đề cho phép (ví dụ câu ngắn nhưng đọc chậm), phần dư sẽ được cộng đè lên phần đầu của câu kế tiếp — app sẽ ghi cảnh báo trong log để bạn biết câu nào bị tràn.

## Giới hạn hiện tại

- Chưa tự động tăng tốc độ đọc để "ép" vừa khung thời gian — bạn cần tự canh chỉnh tốc độ (Rate) hoặc rút ngắn câu phụ đề nếu bị cảnh báo tràn nhiều.
- Chỉ dùng giọng SAPI có sẵn trên máy; chưa hỗ trợ các dịch vụ TTS chất lượng cao hơn (Azure, ElevenLabs...).
- Không xử lý các định dạng phụ đề khác ngoài `.srt` (chưa hỗ trợ `.ass`, `.vtt`).
