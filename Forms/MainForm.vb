Imports MaterialSkin
Imports MaterialSkin.Controls

Public Class MainForm
    Inherits MaterialForm
    Public Logout As Boolean

    Public Sub New()
        Text = $"SI-BIKA - {Db.NamaUser} ({Db.Role.ToUpper()})"
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(1100, 700)
        MaterialSkinManager.Instance.AddFormToManage(Me)
        Dim host As New Panel With {.Dock = DockStyle.Fill}
        Dim top As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 50, .FlowDirection = FlowDirection.RightToLeft}
        top.Controls.Add(Btn("KELUAR", Async Function()
                                           Await Db.LogoutAsync()
                                           Logout = True
                                           Close()
                                       End Function, True))
        Select Case Db.Role
            Case "operator" : host.Controls.Add(New OperatorPanel())
            Case "agen" : host.Controls.Add(New AgenPanel())
            Case "logistik" : host.Controls.Add(New LogistikPanel())
            Case "kepala" : host.Controls.Add(New KepalaPanel())
            Case Else : host.Controls.Add(Lbl("Peran tidak dikenal."))
        End Select
        Controls.Add(host)
        Controls.Add(top)
    End Sub
End Class
