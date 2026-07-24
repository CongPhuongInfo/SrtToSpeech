Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Speech.Synthesis
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class Form1
    Inherits Form

    Private txtFilePath As TextBox
    Private btnBrowse As Button

    Private lblProvider As Label
    Private cmbProvider As ComboBox
    Private btnLoadVoices As Button

    Private lblApiKey As Label
    Private txtApiKey As TextBox
    Private lblAzureRegion As Label
    Private txtAzureRegion As TextBox

    Private lblVoice As Label
    Private cmbVoice As ComboBox
    Private lblGender As Label
    Private cmbGender As ComboBox

    Private lblRate As Label
    Private trkRate As TrackBar
    Private lblRateValue As Label
    Private btnConvert As Button

    Private progressBar As ProgressBar
    Private lblStatus As Label

    Private txtLog As TextBox
    Private btnSaveAudio As Button
    Private btnClearLog As Button

    Private lastAudioSamples As Short() = Nothing
    Private lastWarnings As New List(Of String)()
    Private currentProviderVoices As New List(Of VoiceOption)()

    Public Sub New()
        InitializeComponent()
        UpdateProviderFieldsVisibility()
        AddHandler Me.Shown, Async Sub(s, e) Await LoadVoicesAsyncWrapper()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "SrtToSpeechApp - Chuyển phụ đề SRT thành giọng nói"
        Me.Width = 920
        Me.Height = 700
        Me.MinimumSize = New Size(760, 560)
        Me.StartPosition = FormStartPosition.CenterScreen

        ' ---------- Khu vực cấu hình ----------
        Dim topPanel As New Panel()
        topPanel.Dock = DockStyle.Top
        topPanel.Height = 235

        Dim lblFile As New Label()
        lblFile.Text = "File phụ đề (.srt):"
        lblFile.Location = New Point(10, 10)
        lblFile.AutoSize = True

        txtFilePath = New TextBox()
        txtFilePath.Location = New Point(10, 30)
        txtFilePath.Width = 670
        txtFilePath.ReadOnly = True

        btnBrowse = New Button()
        btnBrowse.Text = "Chọn file..."
        btnBrowse.Location = New Point(690, 28)
        btnBrowse.Width = 110
        btnBrowse.Height = 26
        AddHandler btnBrowse.Click, AddressOf btnBrowse_Click

        ' Nhà cung cấp giọng đọc
        lblProvider = New Label()
        lblProvider.Text = "Nhà cung cấp giọng đọc:"
        lblProvider.Location = New Point(10, 64)
        lblProvider.AutoSize = True

        cmbProvider = New ComboBox()
        cmbProvider.DropDownStyle = ComboBoxStyle.DropDownList
        cmbProvider.Location = New Point(10, 82)
        cmbProvider.Width = 260
        cmbProvider.Items.AddRange(New String() {
            "SAPI (offline, miễn phí)",
            "Google Cloud TTS (cần API key)",
            "Azure Speech (cần API key)",
            "Edge TTS (miễn phí, không chính thức)",
            "ElevenLabs (cần API key)"
        })
        cmbProvider.SelectedIndex = 0
        AddHandler cmbProvider.SelectedIndexChanged, AddressOf cmbProvider_SelectedIndexChanged

        btnLoadVoices = New Button()
        btnLoadVoices.Text = "Tải danh sách giọng đọc"
        btnLoadVoices.Location = New Point(280, 80)
        btnLoadVoices.Width = 190
        AddHandler btnLoadVoices.Click, AddressOf btnLoadVoices_Click

        ' API key / region (chỉ hiện với Google/Azure)
        lblApiKey = New Label()
        lblApiKey.Text = "API Key:"
        lblApiKey.Location = New Point(10, 114)
        lblApiKey.AutoSize = True

        txtApiKey = New TextBox()
        txtApiKey.Location = New Point(10, 132)
        txtApiKey.Width = 340
        txtApiKey.UseSystemPasswordChar = True

        lblAzureRegion = New Label()
        lblAzureRegion.Text = "Region (vd: eastus):"
        lblAzureRegion.Location = New Point(360, 114)
        lblAzureRegion.AutoSize = True

        txtAzureRegion = New TextBox()
        txtAzureRegion.Location = New Point(360, 132)
        txtAzureRegion.Width = 150

        ' Giọng đọc + giới tính
        lblVoice = New Label()
        lblVoice.Text = "Giọng đọc:"
        lblVoice.Location = New Point(10, 166)
        lblVoice.AutoSize = True

        cmbVoice = New ComboBox()
        cmbVoice.DropDownStyle = ComboBoxStyle.DropDownList
        cmbVoice.Location = New Point(10, 184)
        cmbVoice.Width = 470

        lblGender = New Label()
        lblGender.Text = "Giới tính:"
        lblGender.Location = New Point(490, 166)
        lblGender.AutoSize = True

        cmbGender = New ComboBox()
        cmbGender.DropDownStyle = ComboBoxStyle.DropDownList
        cmbGender.Location = New Point(490, 184)
        cmbGender.Width = 90
        cmbGender.Items.AddRange(New String() {"Tất cả", "Nam", "Nữ"})
        cmbGender.SelectedIndex = 0
        AddHandler cmbGender.SelectedIndexChanged, AddressOf cmbGender_SelectedIndexChanged

        ' Tốc độ đọc + nút chuyển đổi
        lblRate = New Label()
        lblRate.Text = "Tốc độ đọc:"
        lblRate.Location = New Point(10, 208)
        lblRate.AutoSize = True

        trkRate = New TrackBar()
        trkRate.Location = New Point(100, 200)
        trkRate.Width = 250
        trkRate.Minimum = -10
        trkRate.Maximum = 10
        trkRate.Value = 0
        trkRate.TickFrequency = 1
        AddHandler trkRate.ValueChanged, AddressOf trkRate_ValueChanged

        lblRateValue = New Label()
        lblRateValue.Text = "0"
        lblRateValue.Location = New Point(360, 208)
        lblRateValue.AutoSize = True

        btnConvert = New Button()
        btnConvert.Text = "Chuyển đổi"
        btnConvert.Location = New Point(690, 198)
        btnConvert.Width = 110
        btnConvert.Height = 32
        AddHandler btnConvert.Click, AddressOf btnConvert_Click

        topPanel.Controls.Add(lblFile)
        topPanel.Controls.Add(txtFilePath)
        topPanel.Controls.Add(btnBrowse)
        topPanel.Controls.Add(lblProvider)
        topPanel.Controls.Add(cmbProvider)
        topPanel.Controls.Add(btnLoadVoices)
        topPanel.Controls.Add(lblApiKey)
        topPanel.Controls.Add(txtApiKey)
        topPanel.Controls.Add(lblAzureRegion)
        topPanel.Controls.Add(txtAzureRegion)
        topPanel.Controls.Add(lblVoice)
        topPanel.Controls.Add(cmbVoice)
        topPanel.Controls.Add(lblGender)
        topPanel.Controls.Add(cmbGender)
        topPanel.Controls.Add(lblRate)
        topPanel.Controls.Add(trkRate)
        topPanel.Controls.Add(lblRateValue)
        topPanel.Controls.Add(btnConvert)

        ' ---------- Thanh tiến trình + trạng thái ----------
        Dim statusPanel As New Panel()
        statusPanel.Dock = DockStyle.Top
        statusPanel.Height = 45

        progressBar = New ProgressBar()
        progressBar.Location = New Point(10, 5)
        progressBar.Width = 880
        progressBar.Height = 18
        progressBar.Minimum = 0
        progressBar.Maximum = 100

        lblStatus = New Label()
        lblStatus.Text = "Sẵn sàng."
        lblStatus.Location = New Point(10, 26)
        lblStatus.AutoSize = True

        statusPanel.Controls.Add(progressBar)
        statusPanel.Controls.Add(lblStatus)

        ' ---------- Nút phía dưới ----------
        Dim bottomPanel As New Panel()
        bottomPanel.Dock = DockStyle.Bottom
        bottomPanel.Height = 42

        btnSaveAudio = New Button()
        btnSaveAudio.Text = "Lưu file âm thanh..."
        btnSaveAudio.Location = New Point(10, 6)
        btnSaveAudio.Width = 150
        btnSaveAudio.Enabled = False
        AddHandler btnSaveAudio.Click, AddressOf btnSaveAudio_Click

        Dim btnClearLogLocal As New Button()
        btnClearLog = btnClearLogLocal
        btnClearLog.Text = "Xóa log"
        btnClearLog.Location = New Point(170, 6)
        btnClearLog.Width = 100
        AddHandler btnClearLog.Click, AddressOf btnClearLog_Click

        bottomPanel.Controls.Add(btnSaveAudio)
        bottomPanel.Controls.Add(btnClearLog)

        ' ---------- Log chi tiết ----------
        Dim lblLog As New Label()
        lblLog.Text = "Log chi tiết:"
        lblLog.Dock = DockStyle.Top
        lblLog.Height = 20
        lblLog.Padding = New Padding(5, 3, 0, 0)

        txtLog = New TextBox()
        txtLog.Multiline = True
        txtLog.ReadOnly = True
        txtLog.ScrollBars = ScrollBars.Vertical
        txtLog.Dock = DockStyle.Fill
        txtLog.Font = New Font("Consolas", 9)
        txtLog.BackColor = Color.Black
        txtLog.ForeColor = Color.LightGreen

        Dim logPanel As New Panel()
        logPanel.Dock = DockStyle.Fill
        logPanel.Controls.Add(txtLog)
        logPanel.Controls.Add(lblLog)

        Me.Controls.Add(logPanel)
        Me.Controls.Add(bottomPanel)
        Me.Controls.Add(statusPanel)
        Me.Controls.Add(topPanel)
    End Sub

    ' ==================== Xử lý nhà cung cấp giọng đọc ====================

    Private Function GetSelectedProvider() As TtsProvider
        Select Case cmbProvider.SelectedIndex
            Case 0 : Return TtsProvider.Sapi
            Case 1 : Return TtsProvider.GoogleCloud
            Case 2 : Return TtsProvider.Azure
            Case 3 : Return TtsProvider.EdgeTts
            Case 4 : Return TtsProvider.ElevenLabs
            Case Else : Return TtsProvider.Sapi
        End Select
    End Function

    Private Function ProviderDisplayName(p As TtsProvider) As String
        Select Case p
            Case TtsProvider.Sapi : Return "SAPI"
            Case TtsProvider.GoogleCloud : Return "Google Cloud TTS"
            Case TtsProvider.Azure : Return "Azure Speech"
            Case TtsProvider.EdgeTts : Return "Edge TTS"
            Case TtsProvider.ElevenLabs : Return "ElevenLabs"
            Case Else : Return "?"
        End Select
    End Function

    Private Sub UpdateProviderFieldsVisibility()
        Dim provider = GetSelectedProvider()
        Dim needsApiKey As Boolean = (provider = TtsProvider.GoogleCloud OrElse provider = TtsProvider.Azure OrElse provider = TtsProvider.ElevenLabs)
        Dim needsRegion As Boolean = (provider = TtsProvider.Azure)

        lblApiKey.Visible = needsApiKey
        txtApiKey.Visible = needsApiKey
        lblAzureRegion.Visible = needsRegion
        txtAzureRegion.Visible = needsRegion
    End Sub

    Private Async Sub cmbProvider_SelectedIndexChanged(sender As Object, e As EventArgs)
        UpdateProviderFieldsVisibility()
        cmbVoice.Items.Clear()
        currentProviderVoices.Clear()

        Dim provider = GetSelectedProvider()
        If provider = TtsProvider.Sapi OrElse provider = TtsProvider.EdgeTts Then
            ' Không cần API key nên tự tải luôn cho tiện
            Await LoadVoicesAsyncWrapper()
        End If
    End Sub

    Private Async Sub btnLoadVoices_Click(sender As Object, e As EventArgs)
        Await LoadVoicesAsyncWrapper()
    End Sub

    Private Async Function LoadVoicesAsyncWrapper() As Task
        Dim provider = GetSelectedProvider()
        btnLoadVoices.Enabled = False
        lblStatus.Text = "Đang tải danh sách giọng đọc..."

        Try
            Select Case provider
                Case TtsProvider.Sapi
                    currentProviderVoices = Await Task.Run(Function() TtsEngine.GetInstalledVoices())

                Case TtsProvider.GoogleCloud
                    If String.IsNullOrWhiteSpace(txtApiKey.Text) Then
                        MessageBox.Show("Vui lòng nhập API Key cho Google Cloud TTS.", "Thiếu API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                        Return
                    End If
                    currentProviderVoices = Await CloudTts.GetGoogleVoicesAsync(txtApiKey.Text.Trim())

                Case TtsProvider.Azure
                    If String.IsNullOrWhiteSpace(txtApiKey.Text) OrElse String.IsNullOrWhiteSpace(txtAzureRegion.Text) Then
                        MessageBox.Show("Vui lòng nhập API Key và Region cho Azure Speech.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                        Return
                    End If
                    currentProviderVoices = Await CloudTts.GetAzureVoicesAsync(txtApiKey.Text.Trim(), txtAzureRegion.Text.Trim())

                Case TtsProvider.EdgeTts
                    currentProviderVoices = Await CloudTts.GetEdgeVoicesAsync()

                Case TtsProvider.ElevenLabs
                    If String.IsNullOrWhiteSpace(txtApiKey.Text) Then
                        MessageBox.Show("Vui lòng nhập API Key cho ElevenLabs.", "Thiếu API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                        Return
                    End If
                    currentProviderVoices = Await CloudTts.GetElevenLabsVoicesAsync(txtApiKey.Text.Trim())
            End Select

            ApplyGenderFilter()
            AppendLog($"Tải được {currentProviderVoices.Count} giọng đọc từ {ProviderDisplayName(provider)}.")
            lblStatus.Text = "Sẵn sàng."
        Catch ex As Exception
            AppendLog($"Lỗi khi tải danh sách giọng đọc ({ProviderDisplayName(provider)}): " & ex.Message)
            MessageBox.Show(ex.Message, "Lỗi tải danh sách giọng đọc", MessageBoxButtons.OK, MessageBoxIcon.Error)
            lblStatus.Text = "Lỗi tải danh sách giọng đọc."
        Finally
            btnLoadVoices.Enabled = True
        End Try
    End Function

    Private Sub ApplyGenderFilter()
        cmbVoice.Items.Clear()
        Dim filterText As String = cmbGender.SelectedItem?.ToString()

        Dim filtered As IEnumerable(Of VoiceOption) = currentProviderVoices
        Select Case filterText
            Case "Nam"
                filtered = currentProviderVoices.Where(Function(v) v.Gender = VoiceGender.Male)
            Case "Nữ"
                filtered = currentProviderVoices.Where(Function(v) v.Gender = VoiceGender.Female)
        End Select

        For Each v In filtered
            cmbVoice.Items.Add(v)
        Next

        If cmbVoice.Items.Count > 0 Then cmbVoice.SelectedIndex = 0
    End Sub

    Private Sub cmbGender_SelectedIndexChanged(sender As Object, e As EventArgs)
        ApplyGenderFilter()
    End Sub

    Private Sub trkRate_ValueChanged(sender As Object, e As EventArgs)
        lblRateValue.Text = trkRate.Value.ToString()
    End Sub

    Private Sub btnBrowse_Click(sender As Object, e As EventArgs)
        Using ofd As New OpenFileDialog()
            ofd.Filter = "File phụ đề (*.srt)|*.srt|Tất cả file (*.*)|*.*"
            ofd.Title = "Chọn file phụ đề SRT"
            If ofd.ShowDialog() = DialogResult.OK Then
                txtFilePath.Text = ofd.FileName
            End If
        End Using
    End Sub

    ' ==================== Chuyển đổi ====================

    Private Async Sub btnConvert_Click(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(txtFilePath.Text) OrElse Not IO.File.Exists(txtFilePath.Text) Then
            MessageBox.Show("Vui lòng chọn file .srt hợp lệ.", "Thiếu file", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If cmbVoice.Items.Count = 0 Then
            MessageBox.Show("Chưa có giọng đọc nào được tải. Bấm 'Tải danh sách giọng đọc' trước.", "Thiếu giọng đọc", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim filePath As String = txtFilePath.Text
        Dim selectedVoice As VoiceOption = CType(cmbVoice.SelectedItem, VoiceOption)
        Dim rate As Integer = trkRate.Value
        Dim ratePercent As Integer = rate * 5 ' quy đổi -10..10 sang -50%..+50% cho Azure/Edge
        Dim apiKey As String = txtApiKey.Text.Trim()
        Dim region As String = txtAzureRegion.Text.Trim()

        SetUiEnabled(False)
        txtLog.Clear()
        progressBar.Value = 0
        lblStatus.Text = "Đang xử lý..."
        btnSaveAudio.Enabled = False
        lastAudioSamples = Nothing
        lastWarnings = New List(Of String)()

        Dim progressLog As New Progress(Of String)(AddressOf AppendLog)
        Dim progressPercent As New Progress(Of Integer)(AddressOf UpdateProgress)

        Try
            AppendLog("Đang đọc file SRT...")
            Dim entries As List(Of SrtEntry) = Await Task.Run(Function() SrtParser.Parse(filePath))
            AppendLog($"Đọc được {entries.Count} câu phụ đề.")

            If entries.Count = 0 Then
                AppendLog("File SRT không có nội dung hợp lệ.")
                Return
            End If

            ' Xây dựng hàm tổng hợp giọng nói tương ứng với nhà cung cấp đã chọn
            Dim synthesizeFunc As Func(Of String, Task(Of Short()))
            Select Case selectedVoice.Provider
                Case TtsProvider.Sapi
                    synthesizeFunc = Function(text) Task.Run(Function() TtsEngine.SynthesizeToSamples(text, selectedVoice.Name, rate))
                Case TtsProvider.GoogleCloud
                    synthesizeFunc = Function(text) CloudTts.SynthesizeGoogleAsync(text, selectedVoice.Name, selectedVoice.LanguageCode, apiKey)
                Case TtsProvider.Azure
                    synthesizeFunc = Function(text) CloudTts.SynthesizeAzureAsync(text, selectedVoice.Name, apiKey, region, ratePercent)
                Case TtsProvider.EdgeTts
                    synthesizeFunc = Function(text) CloudTts.SynthesizeEdgeAsync(text, selectedVoice.Name, ratePercent)
                Case TtsProvider.ElevenLabs
                    synthesizeFunc = Function(text) CloudTts.SynthesizeElevenLabsAsync(text, selectedVoice.Name, apiKey)
                    AppendLog("Lưu ý: ElevenLabs hiện chưa hỗ trợ tùy chỉnh tốc độ đọc trong app này, thanh 'Tốc độ đọc' sẽ không áp dụng.")
                Case Else
                    Throw New Exception("Nhà cung cấp giọng đọc không hợp lệ.")
            End Select

            AppendLog($"Đang tổng hợp giọng nói bằng {ProviderDisplayName(selectedVoice.Provider)}...")
            Dim mixResult = Await AudioMixer.BuildTimedAudio(entries, synthesizeFunc, progressLog, progressPercent)

            lastAudioSamples = mixResult.Samples
            lastWarnings = mixResult.Warnings

            If lastWarnings.Count > 0 Then
                AppendLog($"Hoàn tất với {lastWarnings.Count} cảnh báo (một số câu đọc dài hơn khung thời gian phụ đề).")
            Else
                AppendLog("Hoàn tất, không có cảnh báo tràn thời gian.")
            End If

            btnSaveAudio.Enabled = True
            lblStatus.Text = "Hoàn tất."
        Catch ex As Exception
            AppendLog("Lỗi: " & ex.Message)
            lblStatus.Text = "Thất bại: " & ex.Message
            MessageBox.Show(ex.Message, "Lỗi khi chuyển đổi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            SetUiEnabled(True)
        End Try
    End Sub

    Private Sub btnSaveAudio_Click(sender As Object, e As EventArgs)
        If lastAudioSamples Is Nothing Then
            MessageBox.Show("Chưa có dữ liệu âm thanh để lưu.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using sfd As New SaveFileDialog()
            sfd.Filter = "WAV Audio (*.wav)|*.wav"
            sfd.FileName = "giong_doc.wav"
            If sfd.ShowDialog() = DialogResult.OK Then
                AudioMixer.WriteWavFile(lastAudioSamples, sfd.FileName)
                AppendLog("Đã lưu file âm thanh: " & sfd.FileName)
                MessageBox.Show("Đã lưu file âm thanh.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End Using
    End Sub

    Private Sub btnClearLog_Click(sender As Object, e As EventArgs)
        txtLog.Clear()
    End Sub

    Private Sub AppendLog(message As String)
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}")
    End Sub

    Private Sub UpdateProgress(percent As Integer)
        Dim clamped As Integer = Math.Min(progressBar.Maximum, Math.Max(progressBar.Minimum, percent))
        progressBar.Value = clamped
        lblStatus.Text = $"Đang tổng hợp giọng nói... {clamped}%"
    End Sub

    Private Sub SetUiEnabled(enabled As Boolean)
        btnBrowse.Enabled = enabled
        btnConvert.Enabled = enabled
        cmbProvider.Enabled = enabled
        cmbVoice.Enabled = enabled
        cmbGender.Enabled = enabled
        btnLoadVoices.Enabled = enabled
        trkRate.Enabled = enabled
        txtApiKey.Enabled = enabled
        txtAzureRegion.Enabled = enabled
    End Sub

End Class
