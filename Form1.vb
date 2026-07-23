Imports System.Collections.Generic
Imports System.Drawing
Imports System.Speech.Synthesis
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class Form1
    Inherits Form

    Private txtFilePath As TextBox
    Private btnBrowse As Button
    Private lblVoice As Label
    Private cmbVoice As ComboBox
    Private lblGender As Label
    Private cmbGender As ComboBox
    Private btnRefreshVoices As Button
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

    Public Sub New()
        InitializeComponent()
        LoadVoices()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "SrtToSpeechApp - Chuyển phụ đề SRT thành giọng nói"
        Me.Width = 900
        Me.Height = 620
        Me.MinimumSize = New Size(700, 480)
        Me.StartPosition = FormStartPosition.CenterScreen

        ' ---------- Khu vực chọn file + giọng đọc + tốc độ ----------
        Dim topPanel As New Panel()
        topPanel.Dock = DockStyle.Top
        topPanel.Height = 155

        Dim lblFile As New Label()
        lblFile.Text = "File phụ đề (.srt):"
        lblFile.Location = New Point(10, 12)
        lblFile.AutoSize = True

        txtFilePath = New TextBox()
        txtFilePath.Location = New Point(10, 32)
        txtFilePath.Width = 650
        txtFilePath.ReadOnly = True

        btnBrowse = New Button()
        btnBrowse.Text = "Chọn file..."
        btnBrowse.Location = New Point(670, 30)
        btnBrowse.Width = 110
        btnBrowse.Height = 26
        AddHandler btnBrowse.Click, AddressOf btnBrowse_Click

        lblVoice = New Label()
        lblVoice.Text = "Giọng đọc:"
        lblVoice.Location = New Point(10, 68)
        lblVoice.AutoSize = True

        cmbVoice = New ComboBox()
        cmbVoice.DropDownStyle = ComboBoxStyle.DropDownList
        cmbVoice.Location = New Point(10, 86)
        cmbVoice.Width = 300

        lblGender = New Label()
        lblGender.Text = "Giới tính:"
        lblGender.Location = New Point(320, 68)
        lblGender.AutoSize = True

        cmbGender = New ComboBox()
        cmbGender.DropDownStyle = ComboBoxStyle.DropDownList
        cmbGender.Location = New Point(320, 86)
        cmbGender.Width = 90
        cmbGender.Items.AddRange(New String() {"Tất cả", "Nam", "Nữ"})
        cmbGender.SelectedIndex = 0
        AddHandler cmbGender.SelectedIndexChanged, AddressOf cmbGender_SelectedIndexChanged

        btnRefreshVoices = New Button()
        btnRefreshVoices.Text = "Làm mới danh sách"
        btnRefreshVoices.Location = New Point(420, 84)
        btnRefreshVoices.Width = 150
        AddHandler btnRefreshVoices.Click, AddressOf btnRefreshVoices_Click

        lblRate = New Label()
        lblRate.Text = "Tốc độ đọc:"
        lblRate.Location = New Point(10, 118)
        lblRate.AutoSize = True

        trkRate = New TrackBar()
        trkRate.Location = New Point(100, 112)
        trkRate.Width = 250
        trkRate.Minimum = -10
        trkRate.Maximum = 10
        trkRate.Value = 0
        trkRate.TickFrequency = 1
        AddHandler trkRate.ValueChanged, AddressOf trkRate_ValueChanged

        lblRateValue = New Label()
        lblRateValue.Text = "0"
        lblRateValue.Location = New Point(360, 118)
        lblRateValue.AutoSize = True

        btnConvert = New Button()
        btnConvert.Text = "Chuyển đổi"
        btnConvert.Location = New Point(670, 108)
        btnConvert.Width = 110
        btnConvert.Height = 32
        AddHandler btnConvert.Click, AddressOf btnConvert_Click

        topPanel.Controls.Add(lblFile)
        topPanel.Controls.Add(txtFilePath)
        topPanel.Controls.Add(btnBrowse)
        topPanel.Controls.Add(lblVoice)
        topPanel.Controls.Add(cmbVoice)
        topPanel.Controls.Add(lblGender)
        topPanel.Controls.Add(cmbGender)
        topPanel.Controls.Add(btnRefreshVoices)
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
        progressBar.Width = 860
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

        btnClearLog = New Button()
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

    Private Sub LoadVoices()
        cmbVoice.Items.Clear()

        Dim genderFilter As VoiceGender? = Nothing
        Select Case cmbGender.SelectedItem?.ToString()
            Case "Nam"
                genderFilter = VoiceGender.Male
            Case "Nữ"
                genderFilter = VoiceGender.Female
        End Select

        Dim voices As List(Of VoiceOption) = TtsEngine.GetInstalledVoices(genderFilter)

        If voices.Count = 0 Then
            AppendLog("Không tìm thấy giọng đọc nào phù hợp. Vào Settings > Time & Language > Speech để cài thêm giọng, hoặc chọn lại bộ lọc giới tính.")
            Return
        End If

        For Each v In voices
            cmbVoice.Items.Add(v)
        Next
        cmbVoice.SelectedIndex = 0
        AppendLog($"Tìm thấy {voices.Count} giọng đọc phù hợp.")
    End Sub

    Private Sub cmbGender_SelectedIndexChanged(sender As Object, e As EventArgs)
        LoadVoices()
    End Sub

    Private Sub btnRefreshVoices_Click(sender As Object, e As EventArgs)
        LoadVoices()
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

    Private Async Sub btnConvert_Click(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(txtFilePath.Text) OrElse Not IO.File.Exists(txtFilePath.Text) Then
            MessageBox.Show("Vui lòng chọn file .srt hợp lệ.", "Thiếu file", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If cmbVoice.Items.Count = 0 Then
            MessageBox.Show("Máy chưa có giọng đọc nào được cài đặt.", "Thiếu giọng đọc", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim filePath As String = txtFilePath.Text
        Dim selectedVoice As VoiceOption = CType(cmbVoice.SelectedItem, VoiceOption)
        Dim voiceName As String = selectedVoice.Name
        Dim rate As Integer = trkRate.Value

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

            AppendLog("Đang tổng hợp giọng nói theo từng câu...")
            Dim mixResult = Await Task.Run(Function() AudioMixer.BuildTimedAudio(entries, voiceName, rate, progressLog, progressPercent))

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
        cmbVoice.Enabled = enabled
        cmbGender.Enabled = enabled
        btnRefreshVoices.Enabled = enabled
        trkRate.Enabled = enabled
    End Sub

End Class
