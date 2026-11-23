Imports MaterialSkin.Controls
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports LiveChartsCore
Imports LiveChartsCore.SkiaSharpView
Imports LiveChartsCore.SkiaSharpView.WinForms

Public Class KepalaPanel
    Inherits UserControl
    Private ReadOnly lblKpi As New Label With {.Dock = DockStyle.Top, .Height = 150, .Font = New Font("Segoe UI", 11), .Padding = New Padding(10)}
    Private ReadOnly chart As New CartesianChart With {.Dock = DockStyle.Fill}
    Private ReadOnly gKarantina As DataGridView = MakeGrid()
    Private ReadOnly gKoleksi As DataGridView = MakeGrid()
    Private ReadOnly flSel As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 210, .Padding = New Padding(6)}
    Private ReadOnly gInv As DataGridView = MakeGrid()
    Private ReadOnly gMaster As DataGridView = MakeGrid()
    Private ReadOnly cTabel As MaterialComboBox = Combo("Tabel master", 260)
    Private ReadOnly gLap As DataGridView = MakeGrid()
    Private ReadOnly cLap As MaterialComboBox = Combo("Jenis laporan", 280)
    Private ReadOnly gAudit As DataGridView = MakeGrid()
    Private ReadOnly tmr As New Timer With {.Interval = 10000}
    Private ReadOnly t As (tabs As MaterialTabControl, selector As MaterialTabSelector) = NewTabs()
    Private ReadOnly pk As New Dictionary(Of String, String()) From {{"katalog_anomali", {"id"}}, {"item", {"id"}}, {"agen", {"id"}}, {"konfigurasi", {"kunci"}}, {"faktor_skor", {"kategori", "kode"}}, {"tier_config", {"tier"}}}
    Private ReadOnly serial As New HashSet(Of String) From {"katalog_anomali", "item", "agen"}

    Public Sub New()
        Dock = DockStyle.Fill
        ' dashboard
        Dim pd As New Panel With {.Dock = DockStyle.Fill}
        pd.Controls.Add(chart) : pd.Controls.Add(lblKpi)
        Dim bd = Bar() : bd.Controls.Add(Btn("SEGARKAN", AddressOf LoadDashboard, True)) : pd.Controls.Add(bd)
        t.tabs.TabPages.Add(TabPageOf("Dashboard", pd))
        t.tabs.TabPages.Add(TabPageOf("Misi (Operator)", New OperatorPanel()))
        t.tabs.TabPages.Add(TabPageOf("Logistik", New LogistikPanel()))
        ' karantina
        Dim pk1 As New Panel With {.Dock = DockStyle.Fill}
        Dim bk = Bar()
        bk.Controls.Add(Btn("SEGARKAN", AddressOf LoadKarantina, True))
        bk.Controls.Add(Btn("MULAI REHABILITASI", AddressOf Rehab))
        bk.Controls.Add(Btn("PERBARUI SEGEL SEL", AddressOf Segel, True))
        Dim split As New SplitContainer With {.Dock = DockStyle.Fill, .Orientation = Orientation.Horizontal}
        split.Panel1.Controls.Add(gKarantina) : split.Panel2.Controls.Add(gKoleksi)
        pk1.Controls.Add(split) : pk1.Controls.Add(flSel) : pk1.Controls.Add(bk)
        t.tabs.TabPages.Add(TabPageOf("Karantina & Koleksi", pk1))
        ' keuangan
        Dim pf As New Panel With {.Dock = DockStyle.Fill}
        Dim bf = Bar()
        bf.Controls.Add(Btn("SEGARKAN", AddressOf LoadInv, True))
        bf.Controls.Add(Btn("BAYAR LUNAS", AddressOf Lunas))
        bf.Controls.Add(Btn("CETAK INVOICE (PDF)", AddressOf CetakInvoice, True))
        bf.Controls.Add(Btn("CETAK SERTIFIKAT (PDF)", AddressOf CetakSertifikat, True))
        pf.Controls.Add(gInv) : pf.Controls.Add(bf)
        t.tabs.TabPages.Add(TabPageOf("Pembayaran & Invoice", pf))
        ' master
        Dim pm As New Panel With {.Dock = DockStyle.Fill}
        Dim bm = Bar()
        cTabel.Items.AddRange({"item", "katalog_anomali", "agen", "faktor_skor", "tier_config", "konfigurasi"}) : cTabel.SelectedIndex = 0
        bm.Controls.Add(cTabel)
        bm.Controls.Add(Btn("MUAT", AddressOf LoadMaster, True))
        bm.Controls.Add(Btn("TAMBAH", Function() EditMaster(False)))
        bm.Controls.Add(Btn("UBAH", Function() EditMaster(True)))
        pm.Controls.Add(gMaster) : pm.Controls.Add(bm)
        t.tabs.TabPages.Add(TabPageOf("Master Data & Tarif", pm))
        ' laporan
        Dim pl As New Panel With {.Dock = DockStyle.Fill}
        Dim bl = Bar()
        cLap.Items.AddRange({"laporan_kasus", "laporan_keuangan", "laporan_stok", "laporan_agen", "pengguna", "koleksi"}) : cLap.SelectedIndex = 0
        bl.Controls.Add(cLap)
        bl.Controls.Add(Btn("TAMPILKAN", AddressOf LoadLap))
        bl.Controls.Add(Btn("EKSPOR CSV", Function() CsvService.Export(gLap, cLap.Text), True))
        pl.Controls.Add(gLap) : pl.Controls.Add(bl)
        t.tabs.TabPages.Add(TabPageOf("Laporan", pl))
        ' audit
        Dim pa As New Panel With {.Dock = DockStyle.Fill}
        Dim ba = Bar() : ba.Controls.Add(Btn("SEGARKAN", AddressOf LoadAudit, True))
        pa.Controls.Add(gAudit) : pa.Controls.Add(ba)
        t.tabs.TabPages.Add(TabPageOf("Audit", pa))
        Mount(Me, t.tabs, t.selector)
        AddHandler Me.Load, Async Sub(s, e)
                                Await Guard(AddressOf LoadDashboard)
                                Await Guard(AddressOf LoadKarantina)
                                Await Guard(AddressOf LoadInv)
                                Await Guard(AddressOf LoadMaster)
                                tmr.Start()
                            End Sub
        AddHandler tmr.Tick, Async Sub(s, e)
                                 If t.tabs.SelectedTab IsNot Nothing AndAlso t.tabs.SelectedTab.Text.StartsWith("Karantina") Then Await Guard(AddressOf LoadKarantina)
                             End Sub
        AddHandler t.tabs.SelectedIndexChanged, Async Sub(s, e)
                                                    Dim n = t.tabs.SelectedTab.Text
                                                    If n = "Dashboard" Then Await Guard(AddressOf LoadDashboard)
                                                    If n = "Audit" Then Await Guard(AddressOf LoadAudit)
                                                    If n.StartsWith("Pembayaran") Then Await Guard(AddressOf LoadInv)
                                                End Sub
    End Sub

    Private Async Function LoadDashboard() As Task
        Dim r = (Await ListAsync("ringkasan")).Rows(0)
        lblKpi.Text = $"Total kasus: {r("kasus")}   |   Terbengkalai: {r("terbengkalai")}   |   Contain: {r("contain")}   |   Death: {r("death")}" & vbCrLf &
            $"Entitas dikalahkan: {r("dikalahkan")}   |   Ditangkap: {r("ditangkap")}   |   Koleksi jinak: {r("koleksi")}   |   Karantina: {r("karantina")}   |   Antre: {r("antre")}" & vbCrLf &
            $"Pendapatan diterima: {Rp(r("pendapatan"))}   |   Biaya operasional: {Rp(r("biaya"))}   |   KEUNTUNGAN: {Rp(r("keuntungan"))}"
        Dim k = Await ListAsync("laporan_kasus")
        Dim labels = k.Rows.Cast(Of DataRow)().Select(Function(x) "Tier " & x("tier").ToString()).ToArray()
        chart.Series = New ISeries() {
            New ColumnSeries(Of Integer) With {.Name = "Contain", .Values = k.Rows.Cast(Of DataRow)().Select(Function(x) Convert.ToInt32(x("contain"))).ToArray()},
            New ColumnSeries(Of Integer) With {.Name = "Death", .Values = k.Rows.Cast(Of DataRow)().Select(Function(x) Convert.ToInt32(x("death"))).ToArray()},
            New ColumnSeries(Of Integer) With {.Name = "Berjalan", .Values = k.Rows.Cast(Of DataRow)().Select(Function(x) Convert.ToInt32(x("berjalan"))).ToArray()}}
        chart.XAxes = New Axis() {New Axis With {.Labels = labels}}
    End Function

    Private Async Function LoadKarantina() As Task
        Await Rpc("api_rehab_sweep")
        Dim kar = Await ListAsync("karantina")
        Bind(gKarantina, kar)
        Bind(gKoleksi, Await ListAsync("koleksi"))
        flSel.Controls.Clear()
        Dim total = 6
        Dim cfg = Await ListAsync("config")
        For Each c As DataRow In cfg.Rows
            If c("kunci").ToString() = "sel_total" Then total = Convert.ToInt32(c("nilai"))
        Next
        For i = 1 To total
            Dim gb As New GroupBox With {.Text = "SEL " & i, .Size = New Size(190, 90), .Margin = New Padding(4)}
            Dim rows = kar.Select("sel_no=" & i)
            If rows.Length = 0 Then
                gb.Controls.Add(New Label With {.Text = "KOSONG", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter})
            Else
                Dim rw = rows(0)
                Dim pb As New ProgressBar With {.Dock = DockStyle.Bottom, .Maximum = 100, .Value = CInt(Math.Max(0, Math.Min(100, Convert.ToDecimal(rw("segel_persen")))))}
                gb.Controls.Add(New Label With {.Text = $"{rw("nama")}{vbCrLf}{rw("status")}{If(IsDBNull(rw("rehab_persen")), "", " " & rw("rehab_persen") & "%")}{vbCrLf}Segel {rw("segel_persen")}%", .Dock = DockStyle.Fill})
                gb.Controls.Add(pb)
                If Convert.ToDecimal(rw("segel_persen")) < 25 Then gb.BackColor = Color.MistyRose
            End If
            flSel.Controls.Add(gb)
        Next
    End Function

    Private Async Function Rehab() As Task
        Dim r = SelRow(gKarantina)
        Await Rpc("api_rehab_mulai", "p_entitas", Convert.ToInt32(r("id")))
        Await LoadKarantina()
    End Function

    Private Async Function Segel() As Task
        Dim r = SelRow(gKarantina)
        Await Rpc("api_segel_ulang", "p_entitas", Convert.ToInt32(r("id")))
        Await LoadKarantina()
    End Function

    Private Async Function LoadInv() As Task
        Bind(gInv, Await ListAsync("invoice_list"))
    End Function

    Private Async Function Lunas() As Task
        Dim r = SelRow(gInv)
        If MessageBox.Show($"Catat pelunasan {CellText(r, "nomor")} sebesar {Rp(r("sisa"))}?", "Konfirmasi", MessageBoxButtons.YesNo) <> DialogResult.Yes Then Return
        Dim res = Await Rpc("api_bayar_lunas", "p_misi", Convert.ToInt32(r("misi_id")), "p_metode", "Transfer")
        Info("Pembayaran tercatat." & If(res("sertifikat") IsNot Nothing, " Sertifikat " & res("sertifikat").ToString() & " siap dicetak.", ""))
        Await LoadInv()
    End Function

    Private Async Function Detail(misi As Integer) As Task(Of JsonNode)
        Dim n = Await Rpc("api_list", "p_nama", "invoice_detail", "p_args", Params("misi_id", misi))
        Return n.AsArray()(0)
    End Function

    Private Async Function CetakInvoice() As Task
        Dim r = SelRow(gInv)
        Dim d = Await Detail(Convert.ToInt32(r("misi_id")))
        Using sfd As New SaveFileDialog With {.Filter = "PDF|*.pdf", .FileName = CellText(r, "nomor") & ".pdf"}
            If sfd.ShowDialog() <> DialogResult.OK Then Return
            PdfService.Invoice(sfd.FileName, d)
            Process.Start(New ProcessStartInfo(sfd.FileName) With {.UseShellExecute = True})
        End Using
    End Function

    Private Async Function CetakSertifikat() As Task
        Dim r = SelRow(gInv)
        Dim d = Await Detail(Convert.ToInt32(r("misi_id")))
        If d("sertifikat_nomor") Is Nothing Then Throw New BikaException("Sertifikat hanya terbit untuk misi berstatus Contain yang invoice-nya sudah lunas.")
        Using sfd As New SaveFileDialog With {.Filter = "PDF|*.pdf", .FileName = d("sertifikat_nomor").ToString() & ".pdf"}
            If sfd.ShowDialog() <> DialogResult.OK Then Return
            PdfService.Sertifikat(sfd.FileName, d)
            Process.Start(New ProcessStartInfo(sfd.FileName) With {.UseShellExecute = True})
        End Using
    End Function

    Private Async Function LoadMaster() As Task
        Bind(gMaster, Await ListAsync(If(cTabel.Text = "item", "item", MasterList(cTabel.Text))))
    End Function

    Private Function MasterList(tabel As String) As String
        Select Case tabel
            Case "katalog_anomali" : Return "katalog"
            Case "faktor_skor" : Return "faktor"
            Case "tier_config" : Return "tier"
            Case "konfigurasi" : Return "config"
            Case Else : Return tabel
        End Select
    End Function

    Private Async Function EditMaster(edit As Boolean) As Task
        Dim tabel = cTabel.Text
        Dim dt = CType(gMaster.DataSource, DataTable)
        If dt Is Nothing OrElse dt.Columns.Count = 0 Then Throw New BikaException("Tabel kosong: tambahkan lewat SQL Editor atau muat ulang.")
        Dim row As DataRowView = If(edit, SelRow(gMaster), Nothing)
        Dim skip = {"stok_tersedia", "stok_dipinjam", "label", "dibuat_at", "total"}
        Using d As New DlgForm((If(edit, "Ubah ", "Tambah ")) & tabel, 600, 720)
            Dim boxes As New Dictionary(Of String, Control)
            For Each c As DataColumn In dt.Columns
                If skip.Contains(c.ColumnName) OrElse (tabel = "katalog_anomali" AndAlso c.ColumnName = "label") Then Continue For
                If Not edit AndAlso serial.Contains(tabel) AndAlso c.ColumnName = "id" Then Continue For
                Dim isKey = pk(tabel).Contains(c.ColumnName)
                If c.DataType Is GetType(Boolean) Then
                    Dim cb As New MaterialCheckbox With {.Text = c.ColumnName, .AutoSize = True, .Checked = edit AndAlso Not IsDBNull(row(c.ColumnName)) AndAlso CBool(row(c.ColumnName))}
                    If Not edit Then cb.Checked = True
                    boxes(c.ColumnName) = cb : d.Body.Controls.Add(cb)
                Else
                    Dim tb = Txt(c.ColumnName & If(isKey, " (kunci)", ""), 540)
                    If edit Then tb.Text = CellText(row, c.ColumnName)
                    If edit AndAlso isKey Then tb.Enabled = False
                    boxes(c.ColumnName) = tb : d.Body.Controls.Add(tb)
                End If
            Next
            d.OkAction = Async Function()
                             Dim data As New Dictionary(Of String, Object)
                             For Each kv In boxes
                                 Dim col = dt.Columns(kv.Key)
                                 If TypeOf kv.Value Is MaterialCheckbox Then
                                     data(kv.Key) = CType(kv.Value, MaterialCheckbox).Checked
                                     Continue For
                                 End If
                                 Dim s = CType(kv.Value, MaterialTextBox2).Text.Trim()
                                 If s = "" Then
                                     If pk(tabel).Contains(kv.Key) Then Throw New BikaException("Kolom kunci " & kv.Key & " wajib diisi.")
                                     data(kv.Key) = Nothing
                                 ElseIf col.DataType Is GetType(Decimal) Then
                                     data(kv.Key) = Num(s, kv.Key)
                                 ElseIf s.StartsWith("[") OrElse s.StartsWith("{") Then
                                     data(kv.Key) = JsonSerializer.Deserialize(Of JsonElement)(s)
                                 Else
                                     data(kv.Key) = s
                                 End If
                             Next
                             If edit AndAlso serial.Contains(tabel) Then data("id") = Convert.ToInt32(row("id"))
                             Await Rpc("api_master_simpan", "p_tabel", tabel, "p_data", data)
                         End Function
            If d.ShowDialog() = DialogResult.OK Then Await LoadMaster()
        End Using
    End Function

    Private Async Function LoadLap() As Task
        Bind(gLap, Await ListAsync(cLap.Text))
    End Function

    Private Sub InitializeComponent()

    End Sub

    Private Async Function LoadAudit() As Task
        Bind(gAudit, Await ListAsync("audit"))
    End Function
End Class
