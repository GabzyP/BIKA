Imports MaterialSkin
Imports MaterialSkin.Controls
Imports FontAwesome.Sharp

Public Class LoginForm
    Inherits MaterialForm
    Private ReadOnly tEmail As MaterialTextBox2 = Txt("Email", 420)
    Private ReadOnly tPass As MaterialTextBox2 = Txt("Password", 420, True)
    Private ReadOnly btnLogin As MaterialButton
    Private busy As Boolean

    Public Sub New()
        Text = "SI-BIKA - Biro Investigasi & Karantina Anomali"
        ClientSize = New Size(900, 680)
        MinimumSize = New Size(760, 620)
        StartPosition = FormStartPosition.CenterScreen
        Sizable = True
        MaterialSkinManager.Instance.AddFormToManage(Me)
        Dim canvas As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(245, 247, 248)}
        Dim card As New Panel With {.Size = New Size(540, 500), .Anchor = AnchorStyles.None, .BackColor = Color.White, .Padding = New Padding(32), .BorderStyle = BorderStyle.FixedSingle}
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6, .Padding = New Padding(8, 4, 8, 4)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For Each percent As Single In {18, 12, 14, 17, 17, 22}
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, percent))
        Next

        Dim icon As New IconPictureBox With {.IconChar = IconChar.Ghost, .IconColor = Color.FromArgb(55, 71, 79), .IconSize = 64, .Size = New Size(72, 72), .Anchor = AnchorStyles.None}
        Dim heading As New Label With {.Text = "Selamat Datang", .AutoSize = True, .Anchor = AnchorStyles.None, .Font = New Font("Segoe UI", 18, FontStyle.Bold), .ForeColor = Color.FromArgb(55, 71, 79)}
        Dim caption As New Label With {.Text = "Masuk menggunakan akun BIKA Anda", .AutoSize = True, .Anchor = AnchorStyles.None, .Font = New Font("Segoe UI", 10), .ForeColor = Color.FromArgb(90, 100, 105)}
        tEmail.Dock = DockStyle.Fill
        tPass.Dock = DockStyle.Fill
        tEmail.Margin = New Padding(0, 8, 0, 8)
        tPass.Margin = New Padding(0, 8, 0, 8)
        btnLogin = Btn("MASUK", AddressOf DoLogin)
        btnLogin.Dock = DockStyle.Fill
        btnLogin.Margin = New Padding(0, 12, 0, 4)

        layout.Controls.Add(icon, 0, 0)
        layout.Controls.Add(heading, 0, 1)
        layout.Controls.Add(caption, 0, 2)
        layout.Controls.Add(tEmail, 0, 3)
        layout.Controls.Add(tPass, 0, 4)
        layout.Controls.Add(btnLogin, 0, 5)
        card.Controls.Add(layout)
        canvas.Controls.Add(card)
        Controls.Add(canvas)
        AddHandler canvas.Resize, Sub(s, e)
                                      card.Left = Math.Max(0, (canvas.ClientSize.Width - card.Width) \ 2)
                                      card.Top = Math.Max(0, (canvas.ClientSize.Height - card.Height) \ 2)
                                  End Sub
        AddHandler Me.Shown, Sub(s, e) tEmail.Focus()
        AcceptButton = btnLogin
    End Sub

    Private Async Function DoLogin() As Task
        If busy Then Return
        If tEmail.Text.Trim() = "" OrElse tPass.Text = "" Then Throw New BikaException("Email dan password wajib diisi.")
        busy = True
        Try
            Await Db.LoginAsync(tEmail.Text.Trim(), tPass.Text)
            DialogResult = DialogResult.OK
        Finally
            busy = False
        End Try
    End Function
End Class
