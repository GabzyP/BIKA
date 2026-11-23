Imports MaterialSkin
Imports MaterialSkin.Controls
Imports System.Text.RegularExpressions

Public Module UiKit
    Public Async Function Guard(act As Func(Of Task)) As Task
        Try
            Await act()
        Catch ex As BikaException
            MessageBox.Show(ex.Message, "SI-BIKA", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            MessageBox.Show("Terjadi kesalahan: " & ex.Message, "SI-BIKA", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Function

    Public Async Function TryRun(act As Func(Of Task)) As Task(Of Boolean)
        Try
            Await act()
            Return True
        Catch ex As BikaException
            MessageBox.Show(ex.Message, "SI-BIKA", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            MessageBox.Show("Terjadi kesalahan: " & ex.Message, "SI-BIKA", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
        Return False
    End Function

    Public Sub Info(msg As String)
        MessageBox.Show(msg, "SI-BIKA", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Public Function Btn(text As String, act As Func(Of Task), Optional outlined As Boolean = False) As MaterialButton
        Dim b As New MaterialButton With {.Text = text, .AutoSize = True, .Margin = New Padding(4)}
        If outlined Then b.Type = MaterialButton.MaterialButtonType.Outlined
        AddHandler b.Click, Async Sub(s, e) Await Guard(act)
        Return b
    End Function

    Public Function Txt(hint As String, Optional w As Integer = 240, Optional pass As Boolean = False) As MaterialTextBox2
        Return New MaterialTextBox2 With {.Hint = hint, .Width = w, .Margin = New Padding(4), .UseSystemPasswordChar = pass}
    End Function

    Public Function Combo(hint As String, Optional w As Integer = 240) As MaterialComboBox
        Return New MaterialComboBox With {.Hint = hint, .Width = w, .Margin = New Padding(4), .DropDownStyle = ComboBoxStyle.DropDownList}
    End Function

    Public Function Lbl(text As String, Optional bold As Boolean = False) As Label
        Dim l As New Label With {.Text = text, .AutoSize = True, .Margin = New Padding(6), .Font = New Font("Segoe UI", 10, If(bold, FontStyle.Bold, FontStyle.Regular))}
        Return l
    End Function

    Public Function Bar() As FlowLayoutPanel
        Return New FlowLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .WrapContents = True, .Padding = New Padding(4)}
    End Function

    Public Function MakeGrid(Optional editable As Boolean = False) As DataGridView
        Dim g As New DataGridView With {.Dock = DockStyle.Fill, .ReadOnly = Not editable, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect, .MultiSelect = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            .BackgroundColor = Color.White, .BorderStyle = BorderStyle.None, .RowHeadersVisible = False, .EnableHeadersVisualStyles = False}
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55, 71, 79)
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
        g.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9, FontStyle.Bold)
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 248)
        Return g
    End Function

    Public Sub Bind(g As DataGridView, dt As DataTable)
        g.DataSource = dt
        For Each c As DataGridViewColumn In g.Columns
            c.HeaderText = Regex.Replace(c.Name, "_", " ").ToUpper()
            If c.ValueType Is GetType(Decimal) Then
                If Regex.IsMatch(c.Name, "biaya|tarif|total|harga|estimasi|uang|sisa|pendapatan|untung|diterima|jasa|nilai") Then c.DefaultCellStyle.Format = "N0" Else c.DefaultCellStyle.Format = "0.##"
            ElseIf c.ValueType Is GetType(DateTime) Then
                c.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm"
            End If
        Next
        If g.Columns.Contains("terbengkalai") Then
            For Each r As DataGridViewRow In g.Rows
                If Not IsDBNull(r.Cells("terbengkalai").Value) AndAlso CBool(r.Cells("terbengkalai").Value) Then r.DefaultCellStyle.BackColor = Color.MistyRose
            Next
        End If
    End Sub

    Public Function SelRow(g As DataGridView) As DataRowView
        If g.CurrentRow Is Nothing OrElse g.CurrentRow.DataBoundItem Is Nothing Then Throw New BikaException("Pilih satu baris pada tabel terlebih dahulu.")
        Return CType(g.CurrentRow.DataBoundItem, DataRowView)
    End Function

    Public Function CellText(r As DataRowView, col As String) As String
        Return If(IsDBNull(r(col)), "", r(col).ToString())
    End Function

    Public Function ComboVal(c As MaterialComboBox) As Integer
        If c.SelectedValue Is Nothing Then Throw New BikaException("Pilihan '" & c.Hint & "' wajib diisi.")
        Return Convert.ToInt32(c.SelectedValue)
    End Function

    Public Function Num(s As String, nama As String) As Decimal
        Dim d As Decimal
        If Not Decimal.TryParse(s, Globalization.NumberStyles.Number, Globalization.CultureInfo.InvariantCulture, d) Then Throw New BikaException(nama & " harus berupa angka (gunakan titik untuk desimal, tanpa pemisah ribuan).")
        Return d
    End Function

    Public Function TabPageOf(title As String, ctl As Control) As TabPage
        Dim p As New TabPage(title)
        ctl.Dock = DockStyle.Fill
        p.Controls.Add(ctl)
        Return p
    End Function

    Public Function NewTabs() As (tabs As MaterialTabControl, selector As MaterialTabSelector)
        Dim t As New MaterialTabControl With {.Dock = DockStyle.Fill}
        Dim s As New MaterialTabSelector With {.Dock = DockStyle.Top, .BaseTabControl = t}
        Return (t, s)
    End Function

    Public Sub Mount(host As Control, tabs As MaterialTabControl, sel As MaterialTabSelector)
        host.Controls.Add(tabs)
        host.Controls.Add(sel)
    End Sub

    ''' <summary>Dialog input sederhana: kembalikan nilai tiap kolom, atau Nothing bila dibatalkan.</summary>
    Public Function Ask(title As String, ParamArray labels() As String) As String()
        Using d As New DlgForm(title, 480, 140 + labels.Length * 62)
            Dim boxes As New List(Of MaterialTextBox2)
            For Each l In labels
                Dim t = Txt(l, 420)
                boxes.Add(t)
                d.Body.Controls.Add(t)
            Next
            d.OkAction = Function() Task.CompletedTask
            If d.ShowDialog() <> DialogResult.OK Then Return Nothing
            Return boxes.Select(Function(b) b.Text.Trim()).ToArray()
        End Using
    End Function
End Module

Public Class DlgForm
    Inherits MaterialForm
    Public ReadOnly Body As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoScroll = True, .Padding = New Padding(8)}
    Private ReadOnly Foot As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 56, .FlowDirection = FlowDirection.RightToLeft}
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property OkAction As Func(Of Task)

    Public Sub New(title As String, Optional w As Integer = 560, Optional h As Integer = 560)
        Text = title
        Width = w
        Height = h
        StartPosition = FormStartPosition.CenterParent
        Sizable = False
        MaterialSkinManager.Instance.AddFormToManage(Me)
        Controls.Add(Body)
        Controls.Add(Foot)
        Dim ok As New MaterialButton With {.Text = "Simpan", .AutoSize = True}
        Dim cancel As New MaterialButton With {.Text = "Batal", .AutoSize = True, .Type = MaterialButton.MaterialButtonType.Outlined}
        AddHandler ok.Click, Async Sub(s, e)
                                 If Await TryRun(Function() If(OkAction Is Nothing, Task.CompletedTask, OkAction())) Then DialogResult = DialogResult.OK
                             End Sub
        AddHandler cancel.Click, Sub(s, e) DialogResult = DialogResult.Cancel
        Foot.Controls.Add(ok)
        Foot.Controls.Add(cancel)
    End Sub
End Class
