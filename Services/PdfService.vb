Imports QuestPDF.Fluent
Imports QuestPDF.Helpers
Imports QuestPDF.Infrastructure
Imports System.Text.Json.Nodes

Public Module PdfService
    Private Function S(n As JsonNode, k As String) As String
        Return If(n(k) Is Nothing, "", n(k).ToString())
    End Function

    Public Sub Invoice(path As String, d As JsonNode)
        QuestPDF.Settings.License = LicenseType.Community
        Document.Create(Sub(c)
                            c.Page(Sub(p)
                                       p.Size(PageSizes.A4)
                                       p.Margin(40)
                                       p.Header().Column(Sub(h)
                                                             h.Item().Text("BIRO INVESTIGASI & KARANTINA ANOMALI (BIKA)").Bold().FontSize(16)
                                                             h.Item().Text("INVOICE " & S(d, "nomor")).FontSize(13)
                                                         End Sub)
                                       p.Content().PaddingVertical(15).Column(Sub(col)
                                                                                  col.Spacing(4)
                                                                                  col.Item().Text("Klien      : " & S(d, "klien"))
                                                                                  col.Item().Text("Properti   : " & S(d, "properti") & " - " & S(d, "alamat"))
                                                                                  col.Item().Text("Klasifikasi: Tier " & S(d, "tier") & " | Status misi: " & S(d, "status_misi"))
                                                                                  col.Item().PaddingTop(10).Table(Sub(t)
                                                                                                                      t.ColumnsDefinition(Sub(cd)
                                                                                                                                              cd.RelativeColumn(2)
                                                                                                                                              cd.RelativeColumn(4)
                                                                                                                                              cd.RelativeColumn(2)
                                                                                                                                          End Sub)
                                                                                                                      t.Cell().Text("Kelompok").Bold()
                                                                                                                      t.Cell().Text("Uraian").Bold()
                                                                                                                      t.Cell().AlignRight().Text("Jumlah").Bold()
                                                                                                                      For Each r In d("rincian").AsArray()
                                                                                                                          t.Cell().Text(S(r, "kelompok"))
                                                                                                                          t.Cell().Text(S(r, "label"))
                                                                                                                          t.Cell().AlignRight().Text(Rp(CDec(S(r, "nilai"))))
                                                                                                                      Next
                                                                                                                  End Sub)
                                                                                  col.Item().PaddingTop(10).AlignRight().Text("TOTAL     : " & Rp(CDec(S(d, "total")))).Bold()
                                                                                  col.Item().AlignRight().Text("Uang muka : " & Rp(CDec(S(d, "uang_muka"))))
                                                                                  col.Item().AlignRight().Text("SISA BAYAR: " & Rp(CDec(S(d, "sisa")))).Bold()
                                                                                  col.Item().PaddingTop(8).Text("Status pembayaran: " & S(d, "status").ToUpper())
                                                                              End Sub)
                                       p.Footer().AlignCenter().Text("Dokumen dihasilkan SI-BIKA")
                                   End Sub)
                        End Sub).GeneratePdf(path)
    End Sub

    Public Sub Sertifikat(path As String, d As JsonNode)
        QuestPDF.Settings.License = LicenseType.Community
        Document.Create(Sub(c)
                            c.Page(Sub(p)
                                       p.Size(PageSizes.A4.Landscape())
                                       p.Margin(50)
                                       p.Content().AlignCenter().AlignMiddle().Column(Sub(col)
                                                                                          col.Spacing(10)
                                                                                          col.Item().AlignCenter().Text("SERTIFIKAT PROPERTI BEBAS GANGGUAN GAIB").Bold().FontSize(24)
                                                                                          col.Item().AlignCenter().Text("No. " & S(d, "sertifikat_nomor")).FontSize(12)
                                                                                          col.Item().AlignCenter().PaddingTop(20).Text("Biro Investigasi & Karantina Anomali (BIKA) menyatakan bahwa properti").FontSize(14)
                                                                                          col.Item().AlignCenter().Text(S(d, "properti")).Bold().FontSize(22)
                                                                                          col.Item().AlignCenter().Text("milik / atas nama klien").FontSize(14)
                                                                                          col.Item().AlignCenter().Text(S(d, "klien")).Bold().FontSize(22)
                                                                                          col.Item().AlignCenter().Text("telah ditangani, dinetralisasi, dan dinyatakan bebas dari gangguan supranatural.").FontSize(14)
                                                                                          col.Item().AlignCenter().PaddingTop(20).Text("Invoice " & S(d, "nomor") & " telah lunas.").FontSize(11)
                                                                                      End Sub)
                                   End Sub)
                        End Sub).GeneratePdf(path)
    End Sub
End Module
