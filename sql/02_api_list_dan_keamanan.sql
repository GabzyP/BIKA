-- View internal (tidak dapat diakses langsung oleh aplikasi)
create view v_misi as
select m.id, l.nomor, k.nama klien, pr.nama properti, pr.alamat, a.tier, m.status::text status, m.agen_id, ag.nama agen,
  m.estimasi_awal, m.uang_muka, m.ditolak, m.alasan_tolak, m.risiko, m.dibuat_at, m.status_at, l.entitas_kelas kelas, kt.nama dugaan_entitas, l.kepanikan, l.deskripsi,
  (m.status in ('Whisper','Unleashed','Salvation','Exorcise') and (m.ditolak or now()-m.status_at > make_interval(mins=>fn_cfg('terbengkalai_menit')::int))) terbengkalai
from misi m join laporan l on l.id=m.laporan_id join klien k on k.id=l.klien_id join properti pr on pr.id=l.properti_id
join analisis a on a.id=m.analisis_id left join agen ag on ag.id=m.agen_id left join katalog_anomali kt on kt.id=l.katalog_id;

create function api_list(p_nama text, p_args jsonb default '{}') returns jsonb language plpgsql stable security definer set search_path=public as $$
declare r peran := fn_role(); res jsonb; v_ok boolean; ag int;
begin
  if r is null then raise exception 'Akun Anda belum memiliki peran aktif.'; end if;
  v_ok := case
    when p_nama in ('config','faktor','tier','katalog','item') then true
    when p_nama in ('misi','agen','agen_cocok') then r in ('operator','kepala')
    when p_nama = 'analisis' then r in ('operator','kepala','agen')
    when p_nama in ('misi_agen','item_misi') then r = 'agen'
    when p_nama in ('stok','mutasi','permintaan','permintaan_item','karantina') then r in ('logistik','kepala')
    else r = 'kepala' end;
  if not v_ok then raise exception 'Akses ditolak untuk "%" (peran %).', p_nama, r; end if;
  select id into ag from agen where profile_id=auth.uid();

  if p_nama='config' then select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select kunci,nilai,ket from konfigurasi order by kunci) t;
  elsif p_nama='faktor' then select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select kategori,kode,label,poin from faktor_skor order by kategori,poin) t;
  elsif p_nama='tier' then select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select * from tier_config order by tier) t;
  elsif p_nama='katalog' then select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select id,nama,kelas,ciri,nama||' ['||kelas||']' label,aktif from katalog_anomali where aktif or r='kepala' order by id) t;
  elsif p_nama='item' then select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select id,nama,jenis,satuan,harga,biaya_pakai,stok_tersedia,stok_dipinjam,ambang_min,kode_k3,aturan,aktif from item where aktif or r='kepala' order by jenis,nama) t;
  elsif p_nama='analisis' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select a.id,a.laporan_id,a.skor_gejala,a.skor_entitas,a.skor_properti,a.skor_panik,a.total,a.tier,a.override_aktif,a.darurat,a.alasan,a.estimasi,a.k3_cek,tc.label,tc.level_min,tc.tarif tarif_dasar from analisis a join misi m on m.id=(p_args->>'misi_id')::int and m.analisis_id=a.id join tier_config tc on tc.tier=a.tier) t;
  elsif p_nama='agen' then select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select id,profile_id,nama,level,spesialisasi,aktif,tersedia,kondisi from agen order by id) t;
  elsif p_nama='agen_cocok' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (
      select a.id, a.nama||' - Lv '||a.level||' - '||coalesce(a.spesialisasi,'-')||case when not a.aktif then ' [nonaktif]' when not a.tersedia then ' [sibuk]' when a.level<tc.level_min then ' [level kurang]' else '' end label,
             (a.aktif and a.tersedia and a.level>=tc.level_min) memenuhi
      from agen a, tier_config tc where tc.tier=(p_args->>'tier')::int order by (a.aktif and a.tersedia and a.level>=tc.level_min) desc, a.level) t;
  elsif p_nama='misi' then
    select coalesce(jsonb_agg(to_jsonb(t) order by t.id desc),'[]') into res from v_misi t
    where (nullif(p_args->>'status','') is null or t.status=p_args->>'status')
      and (nullif(p_args->>'tier','') is null or t.tier=(p_args->>'tier')::int)
      and (nullif(p_args->>'q','') is null or (t.nomor||' '||t.klien||' '||t.properti) ilike '%'||(p_args->>'q')||'%')
      and (nullif(p_args->>'dari','') is null or t.dibuat_at::date>=(p_args->>'dari')::date)
      and (nullif(p_args->>'sampai','') is null or t.dibuat_at::date<=(p_args->>'sampai')::date)
      and (nullif(p_args->>'agen_id','') is null or t.agen_id=(p_args->>'agen_id')::int)
      and (not coalesce((p_args->>'aktif')::boolean,false) or t.status in ('Whisper','Unleashed','Salvation','Exorcise'));
  elsif p_nama='misi_agen' then
    select coalesce(jsonb_agg(to_jsonb(t) order by t.id desc),'[]') into res from (
      select v.id,v.nomor,v.properti,v.alamat,v.tier,v.status,v.ditolak,v.kelas,v.dugaan_entitas,v.risiko,v.status_at,p.status permintaan from v_misi v left join permintaan p on p.misi_id=v.id where v.agen_id=ag) t;
  elsif p_nama='item_misi' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (
      select pi.item_id,i.nama,i.jenis,i.kode_k3,pi.diminta,pi.diserahkan from permintaan_item pi join permintaan p on p.id=pi.permintaan_id join item i on i.id=pi.item_id
      join misi m on m.id=p.misi_id where m.id=(p_args->>'misi_id')::int and m.agen_id=ag order by i.jenis,i.nama) t;
  elsif p_nama='permintaan' then
    select coalesce(jsonb_agg(to_jsonb(t) order by t.id desc),'[]') into res from (
      select p.id,p.misi_id,v.nomor,v.properti,v.tier,v.agen,p.status,p.dibuat_at,(select string_agg(i.nama||' x'||pi.diminta,', ') from permintaan_item pi join item i on i.id=pi.item_id where pi.permintaan_id=p.id) ringkasan
      from permintaan p join v_misi v on v.id=p.misi_id where nullif(p_args->>'status','') is null or p.status=p_args->>'status') t;
  elsif p_nama='permintaan_item' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (
      select pi.item_id,i.nama,i.jenis,pi.diminta,i.stok_tersedia tersedia,pi.diserahkan from permintaan_item pi join permintaan p on p.id=pi.permintaan_id join item i on i.id=pi.item_id where p.misi_id=(p_args->>'misi_id')::int order by i.jenis,i.nama) t;
  elsif p_nama='stok' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select id,nama,jenis,satuan,stok_tersedia,stok_dipinjam,stok_tersedia+stok_dipinjam total,ambang_min,(stok_tersedia<=ambang_min) menipis,kode_k3 from item where aktif order by jenis,nama) t;
  elsif p_nama='mutasi' then
    select coalesce(jsonb_agg(to_jsonb(t) order by t.id desc),'[]') into res from (
      select s.id,s.at waktu,i.nama item,s.jenis,s.jumlah,s.sebelum,s.sesudah,s.alasan,(select nomor from v_misi where id=s.misi_id) misi,(select nama from profiles where id=s.oleh) oleh
      from stok_transaksi s join item i on i.id=s.item_id order by s.id desc limit 1000) t;
  elsif p_nama='karantina' then
    select coalesce(jsonb_agg(to_jsonb(t) order by t.sel_no nulls last,t.id),'[]') into res from (
      select e.id,e.nama,e.kelas,e.tier,e.lokasi,e.sel_no,e.status,
        case when e.lokasi='sel' then greatest(0,round(100-extract(epoch from now()-e.segel_at)/60*fn_cfg('segel_sel_susut_per_menit')))::int end segel_persen,
        case when e.status='Rehabilitasi' then least(100,round(100*extract(epoch from now()-e.rehab_mulai)/greatest(1,extract(epoch from e.rehab_selesai-e.rehab_mulai))))::int end rehab_persen,e.rehab_selesai
      from entitas e where e.lokasi in ('sel','antre')) t;
  elsif p_nama='koleksi' then select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select id,nama,kelas,tier,dibuat_at from entitas where lokasi='koleksi' order by id desc) t;
  elsif p_nama='invoice_list' then
    select coalesce(jsonb_agg(to_jsonb(t) order by t.misi_id desc),'[]') into res from (
      select i.misi_id,i.nomor,v.properti,v.klien,v.status status_misi,i.total,i.uang_muka,i.sisa,i.status status_invoice,i.sertifikat_nomor from invoice i join v_misi v on v.id=i.misi_id) t;
  elsif p_nama='invoice_detail' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (
      select i.nomor,i.terbit_at tanggal,v.klien,v.properti,v.alamat,v.tier,v.status status_misi,i.rincian,i.total,i.uang_muka,i.sisa,i.status,i.sertifikat_nomor,i.lunas_at
      from invoice i join v_misi v on v.id=i.misi_id where i.misi_id=(p_args->>'misi_id')::int) t;
  elsif p_nama='ringkasan' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select
      (select count(*) from misi) kasus,
      (select count(*) from v_misi where terbengkalai) terbengkalai,
      (select count(*) from misi where status='Contain') contain,(select count(*) from misi where status='Death') death,
      (select count(*) from hasil_misi where berhasil) dikalahkan,(select count(*) from hasil_misi where ditangkap) ditangkap,
      (select count(*) from entitas where lokasi='koleksi') koleksi,(select count(*) from entitas where lokasi='sel') karantina,(select count(*) from entitas where lokasi='antre') antre,
      coalesce((select sum(jumlah) from pembayaran),0) pendapatan,coalesce((select sum(biaya_operasional) from invoice),0) biaya,
      coalesce((select sum(jumlah) from pembayaran),0)-coalesce((select sum(biaya_operasional) from invoice),0) keuntungan) t;
  elsif p_nama='laporan_kasus' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (
      select a.tier,count(*) jumlah,count(*) filter (where m.status='Contain') contain,count(*) filter (where m.status='Death') death,count(*) filter (where m.status='Dibatalkan') batal,
             count(*) filter (where m.status in ('Whisper','Unleashed','Salvation','Exorcise')) berjalan
      from misi m join analisis a on a.id=m.analisis_id group by a.tier order by a.tier) t;
  elsif p_nama='laporan_keuangan' then
    select coalesce(jsonb_agg(to_jsonb(t) order by t.misi_id desc),'[]') into res from (
      select v.id misi_id,v.nomor,v.properti,v.status,v.estimasi_awal,
        round((a.estimasi->>'total')::numeric-((a.estimasi->>'tarif')::numeric*fn_cfg('jasa_agen_persen')/100+(a.estimasi->>'barang')::numeric)) estimasi_keuntungan,
        i.total total_final,coalesce((select sum(jumlah) from pembayaran p where p.misi_id=v.id),0) diterima,i.biaya_operasional biaya,
        case when i.id is not null then coalesce((select sum(jumlah) from pembayaran p where p.misi_id=v.id),0)-i.biaya_operasional end keuntungan_final
      from v_misi v join analisis a on a.id=(select analisis_id from misi where id=v.id) left join invoice i on i.misi_id=v.id) t;
  elsif p_nama='laporan_stok' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (
      select i.nama,i.jenis,coalesce(sum(s.jumlah) filter (where s.jenis='masuk'),0) masuk,coalesce(sum(s.jumlah) filter (where s.jenis='serah'),0) diserahkan,
             coalesce(sum(s.jumlah) filter (where s.jenis='kembali'),0) kembali,coalesce(sum(s.jumlah) filter (where s.jenis='pakai'),0) dipakai,
             coalesce(sum(s.jumlah) filter (where s.jenis='koreksi'),0) koreksi,i.stok_tersedia,i.stok_dipinjam
      from item i left join stok_transaksi s on s.item_id=i.id group by i.id order by i.jenis,i.nama) t;
  elsif p_nama='laporan_agen' then
    select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (
      select a.nama,a.level,a.kondisi,count(m.id) misi,count(m.id) filter (where m.status='Contain') berhasil,count(m.id) filter (where m.status='Death') gagal,
             (select count(*) from misi_log l join misi mm on mm.id=l.misi_id where mm.agen_id=a.id and l.alasan like 'Ditolak agen%') penolakan
      from agen a left join misi m on m.agen_id=a.id group by a.id order by a.nama) t;
  elsif p_nama='audit' then select coalesce(jsonb_agg(to_jsonb(t) order by t.id desc),'[]') into res from (select id,at waktu,nama pengguna,aksi,ref,detail from audit order by id desc limit 500) t;
  elsif p_nama='pengguna' then select coalesce(jsonb_agg(to_jsonb(t)),'[]') into res from (select id,nama,role::text role,aktif from profiles order by nama) t;
  else raise exception 'Daftar "%" tidak dikenal.', p_nama; end if;
  return res;
end $$;

-- ---------------------------------------------------------------- keamanan: tutup semua akses langsung
do $$ declare t record; begin
  for t in select tablename from pg_tables where schemaname='public' loop execute format('alter table %I enable row level security', t.tablename); end loop;
end $$;
revoke all on all tables in schema public from anon, authenticated;
revoke all on all sequences in schema public from anon, authenticated;
revoke execute on all functions in schema public from public, anon, authenticated;
do $$ declare f record; begin
  for f in select p.oid::regprocedure as sig from pg_proc p join pg_namespace n on n.oid=p.pronamespace where n.nspname='public' and p.proname like 'api\_%' loop
    execute format('grant execute on function %s to authenticated', f.sig);
  end loop;
end $$;
