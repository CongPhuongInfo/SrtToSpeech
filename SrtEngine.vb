Imports System.Linq
Imports System.Speech.AudioFormat
Imports System.Speech.Synthesis
Imports System.Text.RegularExpressions
Imports NAudio.Wave

' Một dòng phụ đề: số thứ tự, mốc bắt đầu/kết thúc, nội dung.
Public Class SrtEntry
    Public Property Index As Integer
    Public Property Start As TimeSpan
    Public Property [End] As TimeSpan
    Public Property Text As String
End Class

' ==================== Đọc file SRT ====================
Module SrtParser

    Public Function Parse(filePath As String) As List(Of SrtEntry)
        Dim entries As New List(Of SrtEntry)()
        Dim rawText As String = IO.File.ReadAllText(filePath)

        ' Chuẩn hóa xuống dòng, tách từng khối theo dòng trống
        rawText = rawText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
        Dim blocks() As String = Regex.Split(rawText.Trim(), "\n\s*\n")

        For Each block In blocks
            If String.IsNullOrWhiteSpace(block) Then Continue For

            Dim lines() As String = block.Split(New Char() {vbLf(0)}, StringSplitOptions.None)
            If lines.Length < 2 Then Continue For

            Dim lineIdx As Integer = 0
            Dim indexValue As Integer = entries.Count + 1

            ' Dòng đầu thường là số thứ tự (bỏ qua nếu không parse được số)
            If Integer.TryParse(lines(0).Trim(), indexValue) Then
                lineIdx = 1
            End If

            If lineIdx >= lines.Length Then Continue For

            Dim timeLine As String = lines(lineIdx).Trim()
            Dim match = Regex.Match(timeLine, "(\d{2}:\d{2}:\d{2}[,.]\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}[,.]\d{3})")
            If Not match.Success Then Continue For

            Dim startTime As TimeSpan = ParseSrtTime(match.Groups(1).Value)
            Dim endTime As TimeSpan = ParseSrtTime(match.Groups(2).Value)

            Dim textLines As New List(Of String)()
            For i As Integer = lineIdx + 1 To lines.Length - 1
                ' Bỏ các thẻ định dạng kiểu <i>, <b>, {\an8}... đơn giản
                Dim cleaned As String = Regex.Replace(lines(i), "<[^>]+>", "")
                cleaned = Regex.Replace(cleaned, "\{[^}]+\}", "")
                textLines.Add(cleaned.Trim())
            Next

            Dim fullText As String = String.Join(" ", textLines).Trim()
            If fullText = "" Then Continue For

            entries.Add(New SrtEntry With {
                .Index = indexValue,
                .Start = startTime,
                .[End] = endTime,
                .Text = fullText
            })
        Next

        Return entries
    End Function

    Private Function ParseSrtTime(s As String) As TimeSpan
        s = s.Replace(",", ".")
        Return TimeSpan.ParseExact(s, "hh\:mm\:ss\.fff", Globalization.CultureInfo.InvariantCulture)
    End Function

End Module

' ==================== TTS bằng SAPI (System.Speech.Synthesis) ====================

' Thông tin 1 giọng đọc: tên thật (để SelectVoice) + giới tính hiển thị cho người dùng
Public Class VoiceOption
    Public Property Name As String
    Public Property Gender As VoiceGender
    Public Property Culture As Globalization.CultureInfo

    Public Overrides Function ToString() As String
        Dim genderText As String
        Select Case Gender
            Case VoiceGender.Male
                genderText = "Nam"
            Case VoiceGender.Female
                genderText = "Nữ"
            Case Else
                genderText = "Không rõ"
        End Select

        Dim cultureText As String = If(Culture IsNot Nothing, $" - {Culture.DisplayName}", "")
        Return $"{Name}{cultureText} [{genderText}]"
    End Function
End Class

Module TtsEngine

    ' Định dạng âm thanh dùng chung cho toàn bộ quá trình để dễ trộn: 22050Hz, 16-bit, mono
    Public ReadOnly SampleRate As Integer = 22050
    Public ReadOnly BitsPerSample As Integer = 16
    Public ReadOnly Channels As Integer = 1

    ' Lấy toàn bộ giọng đọc đã cài, kèm giới tính. Có thể lọc theo giới tính (Nothing = lấy hết).
    Public Function GetInstalledVoices(Optional genderFilter As VoiceGender? = Nothing) As List(Of VoiceOption)
        Dim result As New List(Of VoiceOption)()
        Using synth As New SpeechSynthesizer()
            For Each v In synth.GetInstalledVoices()
                If Not v.Enabled Then Continue For
                If genderFilter.HasValue AndAlso v.VoiceInfo.Gender <> genderFilter.Value Then Continue For

                result.Add(New VoiceOption With {
                    .Name = v.VoiceInfo.Name,
                    .Gender = v.VoiceInfo.Gender,
                    .Culture = v.VoiceInfo.Culture
                })
            Next
        End Using
        Return result
    End Function

    ' Đọc 1 dòng text, trả về mảng mẫu PCM 16-bit mono (đã giải mã sẵn từ WAV do SAPI tạo ra)
    Public Function SynthesizeToSamples(text As String, voiceName As String, rate As Integer) As Short()
        Dim fmt As New SpeechAudioFormatInfo(SampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono)

        Using synth As New SpeechSynthesizer()
            If Not String.IsNullOrWhiteSpace(voiceName) Then
                Try
                    synth.SelectVoice(voiceName)
                Catch
                    ' Nếu giọng không còn tồn tại, dùng giọng mặc định
                End Try
            End If
            synth.Rate = rate

            Using ms As New IO.MemoryStream()
                synth.SetOutputToAudioStream(ms, fmt)
                synth.Speak(text)
                synth.SetOutputToNull()

                ms.Position = 0
                Using reader As New RawSourceWaveStream(ms, New WaveFormat(SampleRate, BitsPerSample, Channels))
                    Dim byteBuffer(reader.Length - 1) As Byte
                    reader.Read(byteBuffer, 0, byteBuffer.Length)

                    Dim samples(byteBuffer.Length \ 2 - 1) As Short
                    Buffer.BlockCopy(byteBuffer, 0, samples, 0, byteBuffer.Length)
                    Return samples
                End Using
            End Using
        End Using
    End Function

End Module

' ==================== Ghép các đoạn audio vào đúng vị trí thời gian theo SRT ====================
Module AudioMixer

    ' Kết quả trộn: mảng mẫu PCM cuối cùng + danh sách cảnh báo (đoạn đọc bị tràn qua câu kế tiếp)
    Public Class MixResult
        Public Property Samples As Short()
        Public Property Warnings As New List(Of String)()
    End Class

    Public Function BuildTimedAudio(entries As List(Of SrtEntry),
                                     voiceName As String,
                                     rate As Integer,
                                     log As IProgress(Of String),
                                     progress As IProgress(Of Integer)) As MixResult

        Dim result As New MixResult()
        If entries.Count = 0 Then Return result

        ' Tổng thời lượng: lấy mốc kết thúc xa nhất, cộng thêm 1 giây đệm
        Dim totalDuration As TimeSpan = entries.Max(Function(en) en.[End]) + TimeSpan.FromSeconds(1)
        Dim totalSamples As Integer = CInt(totalDuration.TotalSeconds * TtsEngine.SampleRate)
        Dim buffer(totalSamples - 1) As Short

        For i As Integer = 0 To entries.Count - 1
            Dim entry = entries(i)
            log.Report($"[{entry.Index}] ({entry.Start:hh\:mm\:ss}) {entry.Text}")

            Dim segmentSamples As Short() = TtsEngine.SynthesizeToSamples(entry.Text, voiceName, rate)
            Dim segmentDuration As TimeSpan = TimeSpan.FromSeconds(segmentSamples.Length / CDbl(TtsEngine.SampleRate))
            Dim slotDuration As TimeSpan = entry.[End] - entry.Start

            If segmentDuration > slotDuration Then
                Dim overflow As TimeSpan = segmentDuration - slotDuration
                Dim msg As String = $"Câu [{entry.Index}] đọc mất {segmentDuration:hh\:mm\:ss\.ff}, dài hơn khung phụ đề {slotDuration:hh\:mm\:ss\.ff} khoảng {overflow:hh\:mm\:ss\.ff} -> có thể đè lên câu kế tiếp."
                result.Warnings.Add(msg)
                log.Report("CẢNH BÁO: " & msg)
            End If

            Dim startSample As Integer = CInt(entry.Start.TotalSeconds * TtsEngine.SampleRate)

            ' Cộng dồn (mix) vào buffer chính, có giới hạn để tránh tràn số (clipping)
            For s As Integer = 0 To segmentSamples.Length - 1
                Dim targetIndex As Integer = startSample + s
                If targetIndex >= 0 AndAlso targetIndex < buffer.Length Then
                    Dim mixed As Integer = CInt(buffer(targetIndex)) + CInt(segmentSamples(s))
                    mixed = Math.Max(Short.MinValue, Math.Min(Short.MaxValue, mixed))
                    buffer(targetIndex) = CShort(mixed)
                End If
            Next

            progress.Report(CInt((i + 1) / CDbl(entries.Count) * 100))
        Next

        result.Samples = buffer
        Return result
    End Function

    ' Ghi mảng mẫu PCM ra file .wav hoàn chỉnh
    Public Sub WriteWavFile(samples As Short(), outputPath As String)
        Dim format As New WaveFormat(TtsEngine.SampleRate, TtsEngine.BitsPerSample, TtsEngine.Channels)
        Using writer As New WaveFileWriter(outputPath, format)
            Dim byteBuffer(samples.Length * 2 - 1) As Byte
            Buffer.BlockCopy(samples, 0, byteBuffer, 0, byteBuffer.Length)
            writer.Write(byteBuffer, 0, byteBuffer.Length)
        End Using
    End Sub

End Module
