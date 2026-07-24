# SrtToSpeechApp

Ứng dụng WinForms (VB.NET, .NET 9) chuyển file phụ đề `.srt` thành file âm thanh `.wav`, đọc từng câu bằng giọng TTS và **đặt đúng vị trí thời gian** theo mốc timestamp trong file SRT gốc — giống như lồng tiếng tự động theo phụ đề.

Hỗ trợ **4 nhà cung cấp giọng đọc**, chọn linh hoạt ngay trong giao diện:

| Nhà cung cấp | Cần gì | Ghi chú |
|---|---|---|
| **SAPI** (Windows Speech) | Không cần gì, chạy offline | Chất lượng phụ thuộc giọng đã cài trên máy |
| **Google Cloud TTS** | API key (Google Cloud) | Chất lượng cao, có free tier ~1 triệu ký tự/tháng |
| **Azure Speech** | API key + Region | Chất lượng cao, giọng tiếng Việt tự nhiên (vd: `vi-VN-HoaiMyNeural`) |
| **Edge TTS** | Không cần gì, chạy qua internet | Cảnh báo API không chính thức bên dưới |
| **ElevenLabs** | API key | Giọng rất tự nhiên, có free tier giới hạn ký tự/tháng, gọi trực tiếp REST API (không qua NuGet wrapper) |

## Cảnh báo quan trọng về Edge TTS

Edge TTS trong app này gọi vào dịch vụ nội bộ của tính năng "Đọc to văn bản" (Read Aloud) trên Microsoft Edge, **không phải API công khai chính thức** của Microsoft. Nó hoạt động dựa trên việc cộng đồng lập trình viên reverse-engineer giao thức. Vì vậy:

- Có thể **ngừng hoạt động bất cứ lúc nào** nếu Microsoft thay đổi cơ chế xác thực phía server
- Không có SLA, không có hỗ trợ chính thức
- Không nên dùng cho ứng dụng thương mại hoặc công việc quan trọng
- Nếu Edge TTS báo lỗi kết nối/xác thực, đó thường là do Microsoft đã đổi giao thức — hãy chuyển sang Google Cloud TTS hoặc Azure Speech để có giải pháp ổn định lâu dài

## Tính năng

- Đọc file `.srt` chuẩn (số thứ tự, mốc thời gian `-->`, nội dung), tự bỏ qua thẻ định dạng như `<i>`, `<b>`, `{\an8}`...
- Chọn 1 trong 4 nhà cung cấp giọng đọc, tải danh sách giọng theo từng nhà cung cấp
- Lọc giọng đọc theo giới tính: Tất cả / Nam / Nữ
- Chỉnh tốc độ đọc (thanh trượt -10 đến 10, tự quy đổi phù hợp với từng nhà cung cấp)
- Tổng hợp giọng nói cho từng câu rồi **trộn vào đúng mốc thời gian** trong một file âm thanh duy nhất
- Thanh tiến trình theo % số câu đã xử lý, log chi tiết từng câu kèm giờ:phút:giây
- Cảnh báo khi một câu đọc quá dài, vượt khung thời gian của phụ đề đó
- Xuất kết quả ra file `.wav`

## Yêu cầu hệ thống

- Windows
- .NET 9 SDK trở lên (https://dotnet.microsoft.com/download)
- Kết nối internet nếu dùng Google Cloud TTS, Azure Speech, hoặc Edge TTS
- Ít nhất 1 giọng SAPI đã cài trên máy nếu muốn dùng chế độ offline

## Cách lấy API key

### Google Cloud TTS
1. Vào Google Cloud Console (console.cloud.google.com)
2. Tạo project mới (hoặc dùng project có sẵn)
3. Bật API "Cloud Text-to-Speech API"
4. Vào APIs & Services -> Credentials, tạo API Key
5. Dán API key vào ô "API Key" trong app khi chọn Google Cloud TTS

### Azure Speech
1. Vào Azure Portal (portal.azure.com)
2. Tạo resource loại "Speech" (Cognitive Services)
3. Chọn Region lúc tạo (ví dụ eastus, southeastasia...) - ghi nhớ region này
4. Vào resource vừa tạo -> Keys and Endpoint, copy 1 trong 2 key
5. Dán API key vào ô "API Key", điền đúng Region vào ô "Region" trong app

### Edge TTS
Không cần key, chỉ cần bấm "Tải danh sách giọng đọc" khi đã chọn Edge TTS.

### ElevenLabs
1. Đăng ký tài khoản tại elevenlabs.io
2. Vào phần Profile / API Keys, tạo API key
3. Dán API key vào ô "API Key" trong app khi chọn ElevenLabs

Lưu ý: app gọi thẳng REST API chính thức của ElevenLabs (https://api.elevenlabs.io/v1/...), không dùng gói NuGet ElevenLabs-DotNet, để đảm bảo tương thích ổn định lâu dài. Tốc độ đọc (thanh trượt) hiện chưa áp dụng được cho ElevenLabs.

## Cấu trúc project

```
SrtToSpeechApp/
├── Program.vb              Entry point (khởi động WinForms)
├── Form1.vb                 Giao diện chính (code thuần, không dùng designer)
├── SrtEngine.vb              Parse SRT + SAPI TTS + trộn audio theo timeline (provider-agnostic)
├── CloudTts.vb               Tích hợp Google Cloud TTS, Azure Speech, Edge TTS
├── SrtToSpeechApp.vbproj     File project (.NET 9, WinForms)
├── build.bat                 Script build (Release)
└── run.bat                   Script chạy nhanh sau khi build
```

## Cách build & chạy

1. Cài .NET 9 SDK nếu chưa có.
2. Chạy build.bat để restore NuGet packages và build bản Release.
3. Chạy run.bat, hoặc mở trực tiếp:
   bin\Release\net9.0-windows\SrtToSpeechApp.exe

## Cách dùng

1. Bấm "Chọn file..." để chọn file .srt.
2. Chọn "Nhà cung cấp giọng đọc". Nếu chọn Google/Azure, nhập API key (và Region nếu là Azure).
3. Bấm "Tải danh sách giọng đọc" (SAPI/Edge sẽ tự tải khi vừa chọn).
4. Chọn giọng ở ô "Giọng đọc", có thể lọc theo "Giới tính".
5. Chỉnh "Tốc độ đọc" nếu muốn đọc nhanh/chậm hơn mặc định.
6. Bấm "Chuyển đổi", theo dõi tiến trình và log chi tiết.
7. Sau khi hoàn tất, bấm "Lưu file âm thanh..." để xuất ra .wav.

## Cách hoạt động (kỹ thuật)

- Mỗi câu phụ đề được tổng hợp giọng nói riêng, sau đó quy về cùng 1 định dạng chuẩn: 22050Hz / 16-bit / mono để dễ trộn (Google/Azure/Edge trả về định dạng khác sẽ được resample bằng NAudio).
- Toàn bộ file âm thanh cuối cùng là một mảng mẫu (samples) có độ dài bằng tổng thời lượng phụ đề (cộng thêm 1 giây đệm), ban đầu toàn bộ là im lặng.
- Mỗi câu được cộng dồn (mix) vào đúng vị trí mẫu tương ứng với mốc thời gian bắt đầu của câu đó trong SRT.
- Nếu giọng đọc mất nhiều thời gian hơn khung phụ đề cho phép, phần dư sẽ cộng đè lên phần đầu câu kế tiếp - app ghi cảnh báo trong log để bạn biết câu nào bị tràn.
- Kiến trúc tách biệt: SrtEngine.vb không phụ thuộc vào provider cụ thể nào (nhận vào một hàm "đọc 1 câu -> mảng mẫu"), CloudTts.vb chỉ lo việc gọi API và trả PCM chuẩn hóa, Form1.vb chọn hàm tổng hợp phù hợp dựa trên provider của giọng đã chọn.

## Giới hạn hiện tại

- Chưa tự động tăng tốc độ đọc để "ép" vừa khung thời gian - cần tự canh chỉnh tốc độ hoặc rút ngắn câu nếu bị cảnh báo tràn nhiều.
- API key được lưu tạm trong bộ nhớ khi chạy app (ô nhập có che ký tự), không được lưu ra đĩa - mỗi lần mở app cần nhập lại.
- Edge TTS là API không chính thức (xem cảnh báo ở đầu file).
- Không xử lý định dạng phụ đề khác ngoài .srt (chưa hỗ trợ .ass, .vtt).
- Google/Azure tính phí theo ký tự văn bản gửi đi - kiểm tra bảng giá của từng dịch vụ trước khi dùng với file lớn.
