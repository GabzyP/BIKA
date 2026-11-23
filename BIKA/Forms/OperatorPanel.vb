Imports MaterialSkin.Controls
Imports System.Text.Json.Nodes

Public Class OperatorPanel
    Inherits UserControl
    Private ReadOnly tKlien As MaterialTextBox2 = Txt("Nama klien", 300)
    Private ReadOnly tKontak As MaterialTextBox2 = Txt("Kontak klien", 300)
    Private ReadOnly tProp As MaterialTextBox2 = Txt("Nama properti", 300)
    Private ReadOnly tAlamat As MaterialTextBox2 = Txt("Alamat properti", 300)
    Private ReadOnly tDesk As MaterialTextBox2 = Txt("Deskripsi kejadian", 620)
    Private ReadOnly cProp As MaterialComboBox = Combo("Luas & jenis properti", 300)
    Private ReadOnly cEnt As MaterialComboBox = Combo("Dugaan jenis entitas (katalog)", 300)
    Private ReadOnly tbPanik As New TrackBar With {.Minimum = 1, .Maximum = 10, .Value = 5, .Width = 300, .TickFrequency = 1}
    Private ReadOnly lblPanik As Label = Lbl("Kepanikan klien: 5 / 10 (+10 poin)")
    Private ReadOnly gejala As New List(Of MaterialCheckbox)
    Private ReadOnly flGejala As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False}
    Private ReadOnly preview As New Label With {.Dock = DockStyle.Fill, .Font = New Font("Consolas", 10), .Padding = New Padding(10)}
    Private seq As Integer
    ' tab misi
    Private ReadOnly gMisi As DataGridView = MakeGrid()
    Private ReadOnly cStatus As MaterialComboBox = Combo("Status", 150)
    Private ReadOnly cTier As MaterialComboBox = Combo("Tier", 110)
    Private ReadOnly tCari As MaterialTextBox2 = Txt("Cari klien/properti/nomor", 240)
    Private ReadOnly dDari As New DateTimePicker With {.ShowCheckBox = True, .Checked = False, .Format = DateTimePickerFormat.Short, .Width = 130, .Margin = New Padding(8)}
    Private ReadOnly dSampai As New DateTimePicker With {.ShowCheckBox = True, .Checked = False, .Format = DateTimePickerFormat.Short, .Width = 130, .Margin = New Padding(8)}
    Private ReadOnly chkAktif As New MaterialCheckbox With {.Text = "Belum selesai saja", .Checked = True, .AutoSize = True}

    Public Sub New()
        Dock = DockStyle.Fill
        Dim t = NewTabs()
        ' ---- tab laporan baru
        Dim left As New FlowLayoutPanel With {.Dock = DockStyle.Left, .Width = 680, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoScroll = True}
        For Each c As Control In New Control() {tKlien, tKontak, tProp, tAlamat, cProp, cEnt}
            left.Controls.Add(c)
        Next
        left.Controls.Add(lblPanik)
        left.Controls.Add(tbPanik)
        left.Controls.Add(Lbl("Gejala gangguan (akumulatif):", True))
        left.Controls.Add(flGejala)
        left.Controls.Add(tDesk)
        left.Controls.Add(Btn("SIMPAN LAPORAN (WHISPER)", AddressOf SimpanLaporan))
        Dim pnl As New Panel With {.Dock = DockStyle.Fill}
        pnl.Controls.Add(preview)
        pnl.Controls.Add(left)
        t.tabs.TabPages.Add(TabPageOf("Laporan Baru", pnl))
        ' ---- tab misi
        Dim bar1 = Bar()
        For Each c As Control In New Control() {cStatus, cTier, tCari}
            bar1.Controls.Add(c)
        Next
        bar1.Controls.Add(Lbl("Dari:"))
        bar1.Controls.Add(dDari)
        bar1.Controls.Add(Lbl("Sampai:"))
        bar1.Controls.Add(dSampai)
        bar1.Controls.Add(chkAktif)
        bar1.Controls.Add(Btn("CARI", AddressOf LoadMisi))
        bar1.Controls.Add(Btn("TUGASKAN / TUGASKAN ULANG", AddressOf Tugaskan))
        bar1.Controls.Add(Btn("BATALKAN (ADMIN)", AddressOf Batalkan, True))
        Dim pm As New Panel With {.Dock = DockStyle.Fill}
        pm.Controls.Add(gMisi)
        pm.Controls.Add(bar1)
        t.tabs.TabPages.Add(TabPageOf("Daftar Misi / Dashboard", pm))
        Mount(Me, t.tabs, t.selector)
        AddHandler Me.Load, Async Sub(s, e) Await Guard(AddressOf Init)
        AddHandler tbPanik.ValueChanged, Async Sub(s, e)
                                             lblPanik.Text = $"Kepanikan klien: {tbPanik.Value} / 10 (+{tbPanik.Value * 2} poin)"
                                             Await Guard(AddressOf UpdatePreview)
                                         End Sub
        AddHandler cProp.SelectedIndexChanged, Async Sub(s, e) Await Guard(AddressOf UpdatePreview)
        AddHandler cEnt.SelectedIndexChanged, Async Sub(s, e) Await Guard(AddressOf UpdatePreview)
    End Sub

    Private Async Function Init() As Task
        Dim faktor = Await ListAsync("faktor")
        Dim vp = New DataView(faktor, "kategori='properti'", "poin", DataViewRowState.CurrentRows).ToTable()
        cProp.DataSource = vp : cProp.DisplayMember = "label" : cProp.ValueMember = "kode"
        For Each r As DataRow In faktor.Select("kategori='gejala'", "poin")
            Dim c As New MaterialCheckbox With {.Text = $"{r("label")} (+{r("poin")})", .Tag = r("kode").ToString(), .AutoSize = True}
            AddHandler c.CheckedChanged, Async Sub(s, e) Await Guard(AddressOf UpdatePreview)
            gejala.Add(c)
            flGejala.Controls.Add(c)
        Next
        cEnt.DataSource = Await ListAsync("katalog")
        cEnt.DisplayMember = "label" : cEnt.ValueMember = "id"
        cStatus.Items.AddRange({"", "Whisper", "Unleashed", "Salvation", "Exorcise", "Contain", "Death", "Dibatalkan"})
        cTier.Items.AddRange({"", "1", "2", "3", "4", "5"})
        cStatus.SelectedIndex = 0 : cTier.SelectedIndex = 0
        Await UpdatePreview()
        Await LoadMisi()
    End Function

    Private Function Pilihan() As List(Of String)
        Return gejala.Where(Function(c) c.Checked).Select(Function(c) CStr(c.Tag)).ToList()
    End Function

    Private Async Function UpdatePreview() As Task
        If cProp.SelectedValue Is Nothing OrElse cEnt.SelectedValue Is Nothing Then Return
        Dim tiket = Threading.Interlocked.Increment(seq)
        Dim h = Await Rpc("api_preview", "p_gejala", Pilihan(), "p_katalog", Convert.ToInt32(cEnt.SelectedValue), "p_properti", CStr(cProp.SelectedValue), "p_panik", tbPanik.Value)
        If tiket <> seq Then Return
        Dim e = h("estimasi")
        Dim sb As New Text.StringBuilder()
        sb.AppendLine($"TIER {h("tier")} - {h("label")}")
        sb.AppendLine(h("alasan").ToString())
        sb.AppendLine($"Syarat agen: level {h("level_min")}+")
        If CBool(h("darurat").ToString()) Then sb.AppendLine("** Biaya darurat/ekspres berlaku (kepanikan tinggi) **")
        sb.AppendLine()
        sb.AppendLine($"Tarif dasar jasa : {Rp(CDec(e("tarif").ToString()))}")
        sb.AppendLine($"Biaya analisis   : {Rp(CDec(e("analisis").ToString()))}")
        sb.AppendLine($"Biaya darurat    : {Rp(CDec(e("darurat").ToString()))}")
        sb.AppendLine($"Estimasi barang  : {Rp(CDec(e("barang").ToString()))}")
        sb.AppendLine($"ESTIMASI TOTAL   : {Rp(CDec(e("total").ToString()))}")
        sb.AppendLine($"Uang muka        : {Rp(CDec(e("uang_muka").ToString()))}")
        sb.AppendLine()
        sb.AppendLine("Syarat K3 & stok:")
        For Each k In h("k3").AsArray()
            sb.AppendLine($"  [{If(CBool(k("cukup").ToString()), "OK", "KURANG")}] {k("item")} - butuh {k("qty")}, tersedia {k("tersedia")}")
        Next
        preview.Text = sb.ToString()
    End Function

    Private Async Function SimpanLaporan() As Task
        Dim r = Await Rpc("api_laporan_buat", "p_klien", tKlien.Text, "p_kontak", tKontak.Text, "p_properti", tProp.Text, "p_alamat", tAlamat.Text,
                          "p_jenis", CStr(cProp.SelectedValue), "p_katalog", ComboVal(cEnt), "p_panik", tbPanik.Value, "p_gejala", Pilihan(), "p_deskripsi", tDesk.Text)
        Info($"Laporan tersimpan (misi #{r("misi_id")}, Tier {r("tier")}). Lanjutkan dengan menugaskan agen di tab Daftar Misi.")
        For Each c As MaterialTextBox2 In New MaterialTextBox2() {tKlien, tKontak, tProp, tAlamat, tDesk}
            c.Text = ""
        Next
        For Each c In gejala
            c.Checked = False
        Next
        Await LoadMisi()
    End Function

    Private Async Function LoadMisi() As Task
        Dim dt = Await ListAsync("misi", "status", cStatus.Text, "tier", cTier.Text, "q", tCari.Text.Trim(),
            "dari", If(dDari.Checked, dDari.Value.ToString("yyyy-MM-dd"), ""), "sampai", If(dSampai.Checked, dSampai.Value.ToString("yyyy-MM-dd"), ""), "aktif", chkAktif.Checked)
        Bind(gMisi, dt)
    End Function

    Private Async Function Tugaskan() As Task
        Dim row = SelRow(gMisi)
        Dim id = Convert.ToInt32(row("id"))
        Dim st = CellText(row, "status")
        If st <> "Whisper" AndAlso st <> "Unleashed" Then Throw New BikaException("Hanya misi berstatus Whisper/Unleashed yang dapat ditugaskan.")
        Dim an = Await ListAsync("analisis", "misi_id", id)
        If an.Rows.Count = 0 Then Throw New BikaException("Analisis tidak ditemukan.")
        Dim a = an.Rows(0)
        Dim agenDt = Await ListAsync("agen_cocok", "tier", Convert.ToInt32(a("tier")))
        Using d As New DlgForm($"Penugasan misi #{id}", 640, 620)
            Dim lblRingkas As New Label With {.AutoSize = True, .MaximumSize = New Size(580, 0), .Font = New Font("Segoe UI", 10)}
            Dim est = JsonNode.Parse(a("estimasi").ToString())
            Dim total = CDec(est("total").ToString()), um = CDec(est("uang_muka").ToString())
            lblRingkas.Text = $"{a("alasan")}{vbCrLf}Syarat: agen level {a("level_min")}+{vbCrLf}Estimasi budget: {Rp(total)} (jasa {Rp(CDec(est("tarif").ToString()))}, analisis {Rp(CDec(est("analisis").ToString()))}, darurat {Rp(CDec(est("darurat").ToString()))}, barang {Rp(CDec(est("barang").ToString()))}){vbCrLf}Risiko stok K3 saat ini: " & If(CellText(row, "risiko") = "", "-", CellText(row, "risiko"))
            Dim cA = Combo("Agen (sesuai level & ketersediaan)", 580)
            cA.DataSource = agenDt : cA.DisplayMember = "label" : cA.ValueMember = "id"
            Dim tUm = Txt("Uang muka diterima (min. sesuai persentase konfigurasi)", 580)
            tUm.Text = um.ToString("0")
            Dim tAl = Txt("Alasan (wajib untuk penugasan ulang)", 580)
            d.Body.Controls.AddRange(New Control() {lblRingkas, cA, tUm, tAl})
            d.OkAction = Async Function()
                             Dim r = Await Rpc("api_tugaskan", "p_misi", id, "p_agen", ComboVal(cA), "p_uang_muka", Num(tUm.Text, "Uang muka"), "p_alasan", tAl.Text)
                             If r("peringatan") IsNot Nothing Then Info("PERINGATAN K3: " & r("peringatan").ToString() & vbCrLf & "Risiko dicatat pada misi.")
                         End Function
            If d.ShowDialog() = DialogResult.OK Then Await LoadMisi()
        End Using
    End Function

    Private Async Function Batalkan() As Task
        Dim row = SelRow(gMisi)
        Dim a = Ask("Pembatalan administratif", "Alasan pembatalan")
        If a Is Nothing Then Return
        Await Rpc("api_batalkan", "p_misi", Convert.ToInt32(row("id")), "p_alasan", a(0))
        Await LoadMisi()
    End Function
End Class
