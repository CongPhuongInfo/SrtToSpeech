Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Net.WebSockets
Imports System.Speech.Synthesis
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports NAudio.Wave

' ==================== Các nguồn giọng đọc "đám mây" (Google / Azure / Edge) ====================
' Module này KHÔNG phụ thuộc vào SAPI. Mỗi hàm Synthesize trả về mảng mẫu PCM đã được
' quy về cùng 1 định dạng chuẩn (22050Hz / 16-bit / mono) để AudioMixer trộn được với nhau.
Public Module CloudTts

    Private ReadOnly httpClient As New HttpClient()

    ' Token công khai được cộng đồng dùng cho Edge Read Aloud (API không chính thức).
    ' Có thể ngừng hoạt động bất cứ lúc nào nếu Microsoft đổi cơ chế xác thực.
    Private Const EdgeTrustedToken As String = "6A5AA1D4EAFF4E9FB37E23D68491D6F4"

    ' ---------- Chuyển audio thô (WAV hoặc MP3) về định dạng chuẩn để trộn ----------
    Public Function ConvertToStandardSamples(rawAudioBytes As Byte(), isMp3 As Boolean) As Short()
        Using inputStream As New IO.MemoryStream(rawAudioBytes)
            Dim reader As WaveStream = If(isMp3,
                CType(New Mp3FileReader(inputStream), WaveStream),
                CType(New WaveFileReader(inputStream), WaveStream))

            Using reader
                Dim targetFormat As New WaveFormat(TtsEngine.SampleRate, TtsEngine.BitsPerSample, TtsEngine.Channels)
                Dim finalStream As IWaveProvider = reader
                Dim resamplerCreated As MediaFoundationResampler = Nothing

                If reader.WaveFormat.SampleRate <> targetFormat.SampleRate OrElse
                   reader.WaveFormat.BitsPerSample <> targetFormat.BitsPerSample OrElse
                   reader.WaveFormat.Channels <> targetFormat.Channels Then
                    resamplerCreated = New MediaFoundationResampler(reader, targetFormat) With {.ResamplerQuality = 60}
                    finalStream = resamplerCreated
                End If

                Using ms As New IO.MemoryStream()
                    Dim readBuffer(8191) As Byte
                    Dim bytesRead As Integer
                    Do
                        bytesRead = finalStream.Read(readBuffer, 0, readBuffer.Length)
                        If bytesRead > 0 Then ms.Write(readBuffer, 0, bytesRead)
                    Loop While bytesRead > 0

                    If resamplerCreated IsNot Nothing Then resamplerCreated.Dispose()

                    Dim byteArray As Byte() = ms.ToArray()
                    Dim samples(byteArray.Length \ 2 - 1) As Short
                    System.Buffer.BlockCopy(byteArray, 0, samples, 0, byteArray.Length)
                    Return samples
                End Using
            End Using
        End Using
    End Function

    ' ==================== GOOGLE CLOUD TEXT-TO-SPEECH ====================
    ' Docs: https://cloud.google.com/text-to-speech/docs/reference/rest

    Public Async Function GetGoogleVoicesAsync(apiKey As String) As Task(Of List(Of VoiceOption))
        Dim url As String = $"https://texttospeech.googleapis.com/v1/voices?key={Uri.EscapeDataString(apiKey)}"
        Dim responseText As String = Await httpClient.GetStringAsync(url)

        Dim result As New List(Of VoiceOption)()
        Using doc As JsonDocument = JsonDocument.Parse(responseText)
            For Each v In doc.RootElement.GetProperty("voices").EnumerateArray()
                Dim name As String = v.GetProperty("name").GetString()
                Dim langCode As String = v.GetProperty("languageCodes")(0).GetString()
                Dim genderStr As String = v.GetProperty("ssmlGender").GetString()

                Dim gender As VoiceGender = If(genderStr = "MALE", VoiceGender.Male,
                                             If(genderStr = "FEMALE", VoiceGender.Female, VoiceGender.Neutral))

                result.Add(New VoiceOption With {
                    .Name = name,
                    .Gender = gender,
                    .LanguageCode = langCode,
                    .Provider = TtsProvider.GoogleCloud
                })
            Next
        End Using
        Return result
    End Function

    Public Async Function SynthesizeGoogleAsync(text As String, voiceName As String, languageCode As String, apiKey As String) As Task(Of Short())
        Dim url As String = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={Uri.EscapeDataString(apiKey)}"

        Dim requestObj As New Dictionary(Of String, Object) From {
            {"input", New Dictionary(Of String, Object) From {{"text", text}}},
            {"voice", New Dictionary(Of String, Object) From {{"languageCode", languageCode}, {"name", voiceName}}},
            {"audioConfig", New Dictionary(Of String, Object) From {{"audioEncoding", "LINEAR16"}, {"sampleRateHertz", TtsEngine.SampleRate}}}
        }
        Dim bodyJson As String = JsonSerializer.Serialize(requestObj)

        Using content As New StringContent(bodyJson, Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await httpClient.PostAsync(url, content)
            Dim responseText As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Google TTS API lỗi ({CInt(response.StatusCode)}): {responseText}")
            End If

            Using doc As JsonDocument = JsonDocument.Parse(responseText)
                Dim base64Audio As String = doc.RootElement.GetProperty("audioContent").GetString()
                Dim audioBytes As Byte() = Convert.FromBase64String(base64Audio)
                Return ConvertToStandardSamples(audioBytes, isMp3:=False)
            End Using
        End Using
    End Function

    ' ==================== AZURE SPEECH ====================
    ' Docs: https://learn.microsoft.com/azure/ai-services/speech-service/rest-text-to-speech

    Public Async Function GetAzureVoicesAsync(apiKey As String, region As String) As Task(Of List(Of VoiceOption))
        Dim url As String = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/voices/list"
        Using request As New HttpRequestMessage(HttpMethod.Get, url)
            request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey)
            Dim response As HttpResponseMessage = Await httpClient.SendAsync(request)
            Dim responseText As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Azure Speech API lỗi ({CInt(response.StatusCode)}): {responseText}")
            End If

            Dim result As New List(Of VoiceOption)()
            Using doc As JsonDocument = JsonDocument.Parse(responseText)
                For Each v In doc.RootElement.EnumerateArray()
                    Dim shortName As String = v.GetProperty("ShortName").GetString()
                    Dim genderStr As String = v.GetProperty("Gender").GetString()
                    Dim locale As String = v.GetProperty("Locale").GetString()

                    Dim gender As VoiceGender = If(genderStr = "Male", VoiceGender.Male,
                                                 If(genderStr = "Female", VoiceGender.Female, VoiceGender.Neutral))

                    result.Add(New VoiceOption With {
                        .Name = shortName,
                        .Gender = gender,
                        .LanguageCode = locale,
                        .Provider = TtsProvider.Azure
                    })
                Next
            End Using
            Return result
        End Using
    End Function

    Public Async Function SynthesizeAzureAsync(text As String, voiceShortName As String, apiKey As String, region As String, ratePercent As Integer) As Task(Of Short())
        Dim url As String = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1"

        Dim rateAttr As String = If(ratePercent >= 0, $"+{ratePercent}%", $"{ratePercent}%")
        Dim escapedText As String = System.Security.SecurityElement.Escape(text)
        Dim ssml As String = $"<speak version='1.0' xml:lang='en-US'><voice name='{voiceShortName}'><prosody rate='{rateAttr}'>{escapedText}</prosody></voice></speak>"

        Using request As New HttpRequestMessage(HttpMethod.Post, url)
            request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey)
            request.Headers.Add("X-Microsoft-OutputFormat", "riff-22050hz-16bit-mono-pcm")
            request.Headers.Add("User-Agent", "SrtToSpeechApp")
            request.Content = New StringContent(ssml, Encoding.UTF8, "application/ssml+xml")

            Dim response As HttpResponseMessage = Await httpClient.SendAsync(request)
            If Not response.IsSuccessStatusCode Then
                Dim errText As String = Await response.Content.ReadAsStringAsync()
                Throw New Exception($"Azure Speech API lỗi ({CInt(response.StatusCode)}): {errText}")
            End If

            Dim audioBytes As Byte() = Await response.Content.ReadAsByteArrayAsync()
            Return ConvertToStandardSamples(audioBytes, isMp3:=False)
        End Using
    End Function

    ' ==================== ELEVENLABS ====================
    ' Docs: https://elevenlabs.io/docs/api-reference/text-to-speech

    Public Async Function GetElevenLabsVoicesAsync(apiKey As String) As Task(Of List(Of VoiceOption))
        Dim url As String = "https://api.elevenlabs.io/v1/voices"
        Using request As New HttpRequestMessage(HttpMethod.Get, url)
            request.Headers.Add("xi-api-key", apiKey)
            Dim response As HttpResponseMessage = Await httpClient.SendAsync(request)
            Dim responseText As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"ElevenLabs API lỗi ({CInt(response.StatusCode)}): {responseText}")
            End If

            Dim result As New List(Of VoiceOption)()
            Using doc As JsonDocument = JsonDocument.Parse(responseText)
                For Each v In doc.RootElement.GetProperty("voices").EnumerateArray()
                    Dim voiceId As String = v.GetProperty("voice_id").GetString()
                    Dim name As String = v.GetProperty("name").GetString()

                    Dim gender As VoiceGender = VoiceGender.NotSet
                    Dim labelsElement As JsonElement
                    If v.TryGetProperty("labels", labelsElement) AndAlso labelsElement.ValueKind = JsonValueKind.Object Then
                        Dim genderElement As JsonElement
                        If labelsElement.TryGetProperty("gender", genderElement) Then
                            Dim genderStr As String = genderElement.GetString()
                            If genderStr = "male" Then gender = VoiceGender.Male
                            If genderStr = "female" Then gender = VoiceGender.Female
                        End If
                    End If

                    result.Add(New VoiceOption With {
                        .Name = voiceId,
                        .DisplayName = name,
                        .Gender = gender,
                        .LanguageCode = "",
                        .Provider = TtsProvider.ElevenLabs
                    })
                Next
            End Using
            Return result
        End Using
    End Function

    Public Async Function SynthesizeElevenLabsAsync(text As String, voiceId As String, apiKey As String) As Task(Of Short())
        Dim url As String = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}"

        Dim requestObj As New Dictionary(Of String, Object) From {
            {"text", text},
            {"model_id", "eleven_multilingual_v2"},
            {"voice_settings", New Dictionary(Of String, Object) From {{"stability", 0.5}, {"similarity_boost", 0.75}}}
        }
        Dim bodyJson As String = JsonSerializer.Serialize(requestObj)

        Using request As New HttpRequestMessage(HttpMethod.Post, url)
            request.Headers.Add("xi-api-key", apiKey)
            request.Content = New StringContent(bodyJson, Encoding.UTF8, "application/json")

            Dim response As HttpResponseMessage = Await httpClient.SendAsync(request)
            If Not response.IsSuccessStatusCode Then
                Dim errText As String = Await response.Content.ReadAsStringAsync()
                Throw New Exception($"ElevenLabs API lỗi ({CInt(response.StatusCode)}): {errText}")
            End If

            Dim mp3Bytes As Byte() = Await response.Content.ReadAsByteArrayAsync()
            Return ConvertToStandardSamples(mp3Bytes, isMp3:=True)
        End Using
    End Function

    ' ==================== EDGE TTS (KHÔNG CHÍNH THỨC) ====================
    ' Đây là API không công khai của Microsoft Edge Read Aloud, được cộng đồng
    ' reverse-engineer. KHÔNG có SLA, có thể ngừng hoạt động bất cứ lúc nào.

    Public Async Function GetEdgeVoicesAsync() As Task(Of List(Of VoiceOption))
        Dim url As String = $"https://speech.platform.bing.com/consensus/voices/list?trustedclienttoken={EdgeTrustedToken}"
        Dim responseText As String = Await httpClient.GetStringAsync(url)

        Dim result As New List(Of VoiceOption)()
        Using doc As JsonDocument = JsonDocument.Parse(responseText)
            For Each v In doc.RootElement.EnumerateArray()
                Dim shortName As String = v.GetProperty("ShortName").GetString()
                Dim genderStr As String = v.GetProperty("Gender").GetString()
                Dim locale As String = v.GetProperty("Locale").GetString()

                Dim gender As VoiceGender = If(genderStr = "Male", VoiceGender.Male,
                                             If(genderStr = "Female", VoiceGender.Female, VoiceGender.Neutral))

                result.Add(New VoiceOption With {
                    .Name = shortName,
                    .Gender = gender,
                    .LanguageCode = locale,
                    .Provider = TtsProvider.EdgeTts
                })
            Next
        End Using
        Return result
    End Function

    Public Async Function SynthesizeEdgeAsync(text As String, voiceShortName As String, ratePercent As Integer) As Task(Of Short())
        Dim connectionId As String = Guid.NewGuid().ToString("N")
        Dim requestId As String = Guid.NewGuid().ToString("N")
        Dim wsUrl As String = $"wss://speech.platform.bing.com/consensus/speech/synthesize/readaloud/edge/v1?TrustedClientToken={EdgeTrustedToken}&ConnectionId={connectionId}"

        Using ws As New ClientWebSocket()
            Await ws.ConnectAsync(New Uri(wsUrl), CancellationToken.None)

            Dim timestamp As String = DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", Globalization.CultureInfo.InvariantCulture)

            Dim configPayload As New Dictionary(Of String, Object) From {
                {"context", New Dictionary(Of String, Object) From {
                    {"synthesis", New Dictionary(Of String, Object) From {
                        {"audio", New Dictionary(Of String, Object) From {
                            {"metadataoptions", New Dictionary(Of String, Object) From {
                                {"sentenceBoundaryEnabled", False},
                                {"wordBoundaryEnabled", False}
                            }},
                            {"outputFormat", "riff-24khz-16bit-mono-pcm"}
                        }}
                    }}
                }}
            }
            Dim configJson As String = JsonSerializer.Serialize(configPayload)
            Dim configMessage As String = "X-Timestamp:" & timestamp & vbCrLf &
                "Content-Type:application/json; charset=utf-8" & vbCrLf &
                "Path:speech.config" & vbCrLf & vbCrLf & configJson

            Await SendTextAsync(ws, configMessage)

            Dim rateAttr As String = If(ratePercent >= 0, $"+{ratePercent}%", $"{ratePercent}%")
            Dim escapedText As String = System.Security.SecurityElement.Escape(text)
            Dim ssml As String = $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'><voice name='{voiceShortName}'><prosody rate='{rateAttr}'>{escapedText}</prosody></voice></speak>"

            Dim ssmlMessage As String = "X-RequestId:" & requestId & vbCrLf &
                "Content-Type:application/ssml+xml" & vbCrLf &
                "Path:ssml" & vbCrLf & vbCrLf & ssml

            Await SendTextAsync(ws, ssmlMessage)

            Using audioStream As New IO.MemoryStream()
                Dim buffer(65535) As Byte
                Dim finished As Boolean = False

                Do While Not finished
                    Using messageStream As New IO.MemoryStream()
                        Dim recvResult As WebSocketReceiveResult
                        Do
                            recvResult = Await ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), CancellationToken.None)
                            messageStream.Write(buffer, 0, recvResult.Count)
                        Loop While Not recvResult.EndOfMessage

                        Dim messageBytes As Byte() = messageStream.ToArray()

                        If recvResult.MessageType = WebSocketMessageType.Text Then
                            Dim textMsg As String = Encoding.UTF8.GetString(messageBytes)
                            If textMsg.Contains("Path:turn.end") Then
                                finished = True
                            End If
                        ElseIf recvResult.MessageType = WebSocketMessageType.Binary Then
                            If messageBytes.Length > 2 Then
                                Dim headerLen As Integer = (CInt(messageBytes(0)) << 8) Or CInt(messageBytes(1))
                                Dim audioOffset As Integer = 2 + headerLen
                                If audioOffset < messageBytes.Length Then
                                    audioStream.Write(messageBytes, audioOffset, messageBytes.Length - audioOffset)
                                End If
                            End If
                        ElseIf recvResult.MessageType = WebSocketMessageType.Close Then
                            finished = True
                        End If
                    End Using
                Loop

                Try
                    Await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None)
                Catch
                End Try

                Return ConvertToStandardSamples(audioStream.ToArray(), isMp3:=False)
            End Using
        End Using
    End Function

    Private Async Function SendTextAsync(ws As ClientWebSocket, message As String) As Task
        Dim bytes As Byte() = Encoding.UTF8.GetBytes(message)
        Await ws.SendAsync(New ArraySegment(Of Byte)(bytes), WebSocketMessageType.Text, True, CancellationToken.None)
    End Function

End Module
