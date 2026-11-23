create extension if not exists pgcrypto;

create type peran as enum ('operator','agen','logistik','kepala');
create type status_misi as enum ('Whisper','Unleashed','Salvation','Exorcise','Contain','Death','Dibatalkan');

create table profiles(
  id uuid primary key references auth.users(id) on delete cascade,
  nama text not null, role peran not null, aktif boolean not null default true,
  dibuat_at timestamptz not null default now());

create table agen(
  id serial primary key,
  profile_id uuid unique references profiles(id),
  nama text not null,
  level int not null check (level between 1 and 5),
  spesialisasi text, aktif boolean not null default true,
  tersedia boolean not null default true, kondisi text not null default 'Sehat');

create table katalog_anomali(
  id serial primary key, nama text not null,
  kelas text not null check (kelas in ('bayangan','unknown','poltergeist','jin','kuno')),
  ciri text, aktif boolean not null default true);

create table klien(id serial primary key, nama text not null, kontak text);
create table properti(id serial primary key, klien_id int not null references klien, nama text not null, alamat text, jenis text not null);

-- Konfigurasi yang dapat diubah Kepala Biro (BUKAN fakta tetap)
create table konfigurasi(kunci text primary key, nilai numeric not null, ket text);
create table faktor_skor(kategori text not null check (kategori in ('gejala','entitas','properti')), kode text not null, label text not null, poin int not null, primary key(kategori,kode));
create table tier_config(tier int primary key, min_skor int not null, max_skor int not null, label text not null, tarif numeric not null, level_min int not null, rehab_menit int not null, k3 jsonb not null default '[]');

create table item(
  id serial primary key, nama text not null unique,
  jenis text not null check (jenis in ('alat','perlengkapan')),
  satuan text not null default 'buah', harga numeric not null default 0, biaya_pakai numeric not null default 0,
  stok_tersedia int not null default 0 check (stok_tersedia >= 0),
  stok_dipinjam int not null default 0 check (stok_dipinjam >= 0),
  ambang_min int not null default 2, kode_k3 text, aturan text, aktif boolean not null default true);

create table laporan(
  id serial primary key, nomor text unique, klien_id int not null references klien, properti_id int not null references properti,
  katalog_id int references katalog_anomali, entitas_kelas text not null,
  kepanikan int not null check (kepanikan between 1 and 10), deskripsi text,
  dibuat_oleh uuid, dibuat_at timestamptz not null default now());
create table laporan_gejala(laporan_id int references laporan on delete cascade, kode text, primary key(laporan_id,kode));

create table analisis(
  id serial primary key, laporan_id int not null unique references laporan,
  skor_gejala int, skor_entitas int, skor_properti int, skor_panik int, total int, tier int,
  override_aktif boolean, darurat boolean, alasan text,
  snapshot jsonb, estimasi jsonb, k3_cek jsonb, dibuat_at timestamptz not null default now());

create table misi(
  id serial primary key, laporan_id int not null unique references laporan, analisis_id int not null references analisis,
  agen_id int references agen, status status_misi not null default 'Whisper',
  status_at timestamptz not null default now(), status_by uuid,
  ditugaskan_at timestamptz, estimasi_awal numeric not null, uang_muka numeric not null default 0,
  ditolak boolean not null default false, alasan_tolak text, risiko text,
  dibuat_at timestamptz not null default now());
create table misi_log(id bigserial primary key, misi_id int references misi, dari status_misi, ke status_misi, oleh uuid, alasan text, at timestamptz not null default now());

create table permintaan(id serial primary key, misi_id int not null unique references misi, status text not null default 'menunggu', dibuat_at timestamptz not null default now(), diproses_at timestamptz, diproses_oleh uuid);
create table permintaan_item(
  permintaan_id int references permintaan on delete cascade, item_id int references item,
  diminta int not null check (diminta > 0), diserahkan int not null default 0, dipakai int not null default 0, dikembalikan int not null default 0,
  primary key(permintaan_id,item_id));

create table stok_transaksi(
  id bigserial primary key, item_id int not null references item, misi_id int references misi,
  jenis text not null check (jenis in ('masuk','serah','kembali','pakai','koreksi')),
  jumlah int not null, sebelum int not null, sesudah int not null, alasan text, oleh uuid, at timestamptz not null default now());

create table percobaan(
  id serial primary key, misi_id int not null references misi, ke int not null,
  berhasil boolean, durasi int, segel_akhir int, resistensi_akhir int, segel_nol boolean default false,
  pakai jsonb not null default '[]', final boolean not null default false, oleh uuid, at timestamptz not null default now(),
  unique(misi_id,ke));
create unique index uq_percobaan_final on percobaan(misi_id) where final;

create table hasil_misi(
  misi_id int primary key references misi, berhasil boolean not null, entitas_dilawan text, ditangkap boolean not null default false,
  nama_ditangkap text, sifat text, kondisi_agen text, catatan text, segel_nol boolean not null default false,
  percobaan_id int references percobaan, acak jsonb, dibuat_at timestamptz not null default now());

create table entitas(
  id serial primary key, misi_id int references misi, nama text not null, kelas text, tier int,
  sifat text not null check (sifat in ('jinak','jahat')),
  lokasi text not null check (lokasi in ('koleksi','sel','antre')), sel_no int check (sel_no between 1 and 99),
  status text not null, segel_at timestamptz, rehab_mulai timestamptz, rehab_selesai timestamptz, dibuat_at timestamptz not null default now());
create unique index uq_sel on entitas(sel_no) where lokasi='sel';

create table invoice(
  id serial primary key, misi_id int not null unique references misi, nomor text not null unique,
  rincian jsonb not null, total numeric not null, uang_muka numeric not null, sisa numeric not null,
  biaya_operasional numeric not null, status text not null default 'terbit' check (status in ('terbit','lunas')),
  sertifikat_nomor text, terbit_at timestamptz not null default now(), lunas_at timestamptz);
create table pembayaran(id serial primary key, misi_id int not null references misi, jenis text not null check (jenis in ('uang_muka','pelunasan','pengembalian')), jumlah numeric not null, metode text, oleh uuid, at timestamptz not null default now());
create table audit(id bigserial primary key, oleh uuid, nama text, aksi text not null, ref text, detail jsonb, at timestamptz not null default now());

-- ---------------------------------------------------------------- helper
create function fn_role() returns peran language sql stable security definer set search_path=public as
$$ select role from profiles where id=auth.uid() and aktif $$;

create function fn_need(variadic p peran[]) returns void language plpgsql stable security definer set search_path=public as $$
declare r peran := fn_role();
begin
  if r is null then raise exception 'Akun Anda belum memiliki peran aktif.'; end if;
  if r <> 'kepala' and not (r = any(p)) then raise exception 'Akses ditolak untuk peran %.', r; end if;
end $$;

create function fn_cfg(p_kunci text) returns numeric language sql stable security definer set search_path=public as
$$ select nilai from konfigurasi where kunci=p_kunci $$;

create function fn_audit(p_aksi text, p_ref text, p_detail jsonb) returns void language sql security definer set search_path=public as
$$ insert into audit(oleh,nama,aksi,ref,detail) values(auth.uid(),(select nama from profiles where id=auth.uid()),p_aksi,p_ref,p_detail) $$;

create function fn_set_status(p_misi int, p_ke status_misi, p_alasan text) returns void language plpgsql security definer set search_path=public as $$
declare v status_misi;
begin
  select status into v from misi where id=p_misi;
  update misi set status=p_ke, status_at=now(), status_by=auth.uid() where id=p_misi;
  insert into misi_log(misi_id,dari,ke,oleh,alasan) values(p_misi,v,p_ke,auth.uid(),p_alasan);
  perform fn_audit('STATUS','misi:'||p_misi,jsonb_build_object('dari',v,'ke',p_ke,'alasan',p_alasan));
end $$;

-- Satu-satunya jalur perubahan stok: atomik, tanpa saldo negatif, selalu tercatat
create function fn_stok(p_item int, p_jenis text, p_jumlah int, p_dt int, p_dd int, p_misi int, p_alasan text) returns void language plpgsql security definer set search_path=public as $$
declare i item%rowtype;
begin
  select * into i from item where id=p_item for update;
  if not found then raise exception 'Item % tidak ditemukan.', p_item; end if;
  if i.stok_tersedia + p_dt < 0 then raise exception 'Stok % tidak cukup (tersedia %, dibutuhkan %).', i.nama, i.stok_tersedia, -p_dt; end if;
  if i.stok_dipinjam + p_dd < 0 then raise exception 'Saldo pinjaman % tidak valid.', i.nama; end if;
  update item set stok_tersedia=stok_tersedia+p_dt, stok_dipinjam=stok_dipinjam+p_dd where id=p_item;
  insert into stok_transaksi(item_id,misi_id,jenis,jumlah,sebelum,sesudah,alasan,oleh)
  values(p_item,p_misi,p_jenis,p_jumlah,i.stok_tersedia,i.stok_tersedia+p_dt,p_alasan,auth.uid());
end $$;

create function fn_sel_kosong() returns int language sql stable security definer set search_path=public as
$$ select g from generate_series(1,fn_cfg('sel_total')::int) g where g not in (select sel_no from entitas where lokasi='sel') order by g limit 1 $$;

-- ---------------------------------------------------------------- analisis bahaya (Faktor.md)
create function fn_hitung(p_gejala text[], p_kelas text, p_properti text, p_panik int) returns jsonb language plpgsql security definer set search_path=public as $$
declare g text[] := coalesce(p_gejala,'{}'); sg int; se int; sp int; sk int; tot int; t int; ov boolean:=false; tc tier_config%rowtype;
  dar boolean; v_ana numeric; v_dar numeric:=0; v_bar numeric:=0; v_k3 jsonb:='[]'; el jsonb; it item%rowtype; q int; ok boolean;
begin
  select coalesce(sum(poin),0) into sg from faktor_skor where kategori='gejala' and kode = any(g);
  select coalesce(max(poin),0) into se from faktor_skor where kategori='entitas' and kode=p_kelas;
  select coalesce(max(poin),0) into sp from faktor_skor where kategori='properti' and kode=p_properti;
  sk := least(greatest(coalesce(p_panik,1),1),10) * 2;
  tot := sg+se+sp+sk;
  select tier into t from tier_config where tot between min_skor and max_skor;
  if t is null then t := (select max(tier) from tier_config); end if;
  if 'kesurupan' = any(g) and t < 4 then t := 4; ov := true; end if;
  select * into tc from tier_config where tier=t;
  dar := p_panik >= fn_cfg('darurat_ambang');
  v_ana := fn_cfg('biaya_analisis');
  if dar then v_dar := round(tc.tarif*fn_cfg('darurat_persen')/100,0); end if;
  for el in select value from jsonb_array_elements(tc.k3 || '[{"kode":"METERAN","qty":1}]'::jsonb) loop
    q := (el->>'qty')::int;
    select * into it from item where kode_k3=el->>'kode' and aktif order by id limit 1;
    ok := it.id is not null and it.stok_tersedia >= q;
    if it.id is not null then v_bar := v_bar + q*it.biaya_pakai; end if;
    v_k3 := v_k3 || jsonb_build_array(jsonb_build_object('kode',el->>'kode','qty',q,'item_id',it.id,'item',coalesce(it.nama,'(belum dipetakan)'),'tersedia',coalesce(it.stok_tersedia,0),'cukup',ok));
  end loop;
  return jsonb_build_object(
    'skor_gejala',sg,'skor_entitas',se,'skor_properti',sp,'skor_panik',sk,'total',tot,'tier',t,'label',tc.label,
    'level_min',tc.level_min,'override',ov,'darurat',dar,
    'alasan', format('Skor %s = gejala %s + entitas %s + properti %s + kepanikan %s -> Tier %s%s',tot,sg,se,sp,sk,t,case when ov then ' (dipaksa minimal Tier 4: safety override kesurupan)' else '' end),
    'estimasi', jsonb_build_object('tarif',tc.tarif,'analisis',v_ana,'darurat',v_dar,'barang',v_bar,'total',tc.tarif+v_ana+v_dar+v_bar,
                 'uang_muka',round((tc.tarif+v_ana+v_dar+v_bar)*fn_cfg('uang_muka_persen')/100,0)),
    'k3', v_k3,
    'snapshot', jsonb_build_object('faktor',(select jsonb_agg(to_jsonb(f)) from faktor_skor f),'tier',(select jsonb_agg(to_jsonb(x)) from tier_config x),'konfigurasi',(select jsonb_agg(to_jsonb(c)) from konfigurasi c)));
end $$;

create function api_me() returns jsonb language plpgsql stable security definer set search_path=public as $$
declare p profiles%rowtype;
begin
  select * into p from profiles where id=auth.uid() and aktif;
  if not found then raise exception 'Akun belum memiliki profil/peran aktif. Hubungi Kepala Biro.'; end if;
  return jsonb_build_object('nama',p.nama,'role',p.role,'agen_id',(select id from agen where profile_id=p.id));
end $$;

create function api_preview(p_gejala text[], p_katalog int, p_properti text, p_panik int) returns jsonb language plpgsql stable security definer set search_path=public as $$
begin
  perform fn_need('operator');
  return fn_hitung(p_gejala,(select kelas from katalog_anomali where id=p_katalog),p_properti,p_panik);
end $$;

create function api_laporan_buat(p_klien text, p_kontak text, p_properti text, p_alamat text, p_jenis text, p_katalog int, p_panik int, p_gejala text[], p_deskripsi text)
returns jsonb language plpgsql security definer set search_path=public as $$
declare kel text; h jsonb; kid int; pid int; lid int; aid int; mid int;
begin
  perform fn_need('operator');
  if coalesce(trim(p_klien),'')='' or coalesce(trim(p_properti),'')='' then raise exception 'Nama klien dan nama properti wajib diisi.'; end if;
  select kelas into kel from katalog_anomali where id=p_katalog and aktif;
  if kel is null then raise exception 'Pilih dugaan jenis entitas dari katalog.'; end if;
  if not exists(select 1 from faktor_skor where kategori='properti' and kode=p_jenis) then raise exception 'Jenis properti tidak valid.'; end if;
  if coalesce(array_length(p_gejala,1),0)=0 and coalesce(trim(p_deskripsi),'')='' then raise exception 'Centang minimal satu gejala atau isi deskripsi kejadian.'; end if;
  h := fn_hitung(p_gejala,kel,p_jenis,p_panik);
  insert into klien(nama,kontak) values(trim(p_klien),p_kontak) returning id into kid;
  insert into properti(klien_id,nama,alamat,jenis) values(kid,trim(p_properti),p_alamat,p_jenis) returning id into pid;
  lid := nextval(pg_get_serial_sequence('laporan','id'));
  insert into laporan(id,nomor,klien_id,properti_id,katalog_id,entitas_kelas,kepanikan,deskripsi,dibuat_oleh)
  values(lid,'LAP-'||to_char(now(),'YYMMDD')||'-'||lpad(lid::text,4,'0'),kid,pid,p_katalog,kel,p_panik,p_deskripsi,auth.uid());
  insert into laporan_gejala(laporan_id,kode) select lid,unnest(p_gejala);
  insert into analisis(laporan_id,skor_gejala,skor_entitas,skor_properti,skor_panik,total,tier,override_aktif,darurat,alasan,snapshot,estimasi,k3_cek)
  values(lid,(h->>'skor_gejala')::int,(h->>'skor_entitas')::int,(h->>'skor_properti')::int,(h->>'skor_panik')::int,(h->>'total')::int,(h->>'tier')::int,
         (h->>'override')::boolean,(h->>'darurat')::boolean,h->>'alasan',h->'snapshot',h->'estimasi',h->'k3') returning id into aid;
  insert into misi(laporan_id,analisis_id,estimasi_awal,status_by) values(lid,aid,(h->'estimasi'->>'total')::numeric,auth.uid()) returning id into mid;
  insert into misi_log(misi_id,dari,ke,oleh,alasan) values(mid,null,'Whisper',auth.uid(),'Laporan diinput dan dianalisis');
  perform fn_audit('LAPORAN_BARU','misi:'||mid,h-'snapshot');
  return jsonb_build_object('misi_id',mid)||(h-'snapshot');
end $$;

-- ---------------------------------------------------------------- alur misi
create function api_tugaskan(p_misi int, p_agen int, p_uang_muka numeric, p_alasan text default null) returns jsonb language plpgsql security definer set search_path=public as $$
declare m misi%rowtype; an analisis%rowtype; tc tier_config%rowtype; ag agen%rowtype; v_min numeric; v_um numeric; v_risiko text:=''; k jsonb;
begin
  perform fn_need('operator');
  select * into m from misi where id=p_misi for update;
  if not found then raise exception 'Misi tidak ditemukan.'; end if;
  if m.status not in ('Whisper','Unleashed') then raise exception 'Misi berstatus % tidak dapat ditugaskan/ditugaskan ulang.', m.status; end if;
  if m.status='Unleashed' and nullif(trim(p_alasan),'') is null then raise exception 'Penugasan ulang wajib menyertakan alasan.'; end if;
  select * into an from analisis where id=m.analisis_id;
  select * into tc from tier_config where tier=an.tier;
  select * into ag from agen where id=p_agen;
  if not found or not ag.aktif then raise exception 'Agen tidak ditemukan atau tidak aktif.'; end if;
  if not ag.tersedia then raise exception 'Agen % sedang tidak tersedia.', ag.nama; end if;
  if ag.level < tc.level_min then raise exception 'Ditolak: agen % level % tidak memenuhi syarat Tier % (minimal level %).', ag.nama, ag.level, an.tier, tc.level_min; end if;
  select coalesce(sum(jumlah),0) into v_um from pembayaran where misi_id=p_misi and jenis='uang_muka';
  if v_um=0 then
    v_min := round(m.estimasi_awal*fn_cfg('uang_muka_persen')/100,0);
    if coalesce(p_uang_muka,0) < v_min then raise exception 'Uang muka minimal Rp % (sesuai persentase konfigurasi dari estimasi awal).', v_min; end if;
    insert into pembayaran(misi_id,jenis,jumlah,metode,oleh) values(p_misi,'uang_muka',p_uang_muka,'Transfer',auth.uid());
    update misi set uang_muka=p_uang_muka where id=p_misi;
  end if;
  for k in select value from jsonb_array_elements(an.k3_cek) loop
    if k->>'item_id' is null then v_risiko := v_risiko||'Item K3 '||(k->>'kode')||' belum dipetakan. ';
    elsif (select stok_tersedia from item where id=(k->>'item_id')::int) < (k->>'qty')::int then v_risiko := v_risiko||'Stok kurang: '||(k->>'item')||' (butuh '||(k->>'qty')||'). '; end if;
  end loop;
  if m.agen_id is not null and m.agen_id<>p_agen then update agen set tersedia=true where id=m.agen_id and aktif and kondisi in ('Sehat','Luka Ringan'); end if;
  update misi set agen_id=p_agen, ditolak=false, alasan_tolak=null, risiko=nullif(v_risiko,''), ditugaskan_at=now() where id=p_misi;
  update agen set tersedia=false where id=p_agen;
  perform fn_set_status(p_misi,'Unleashed',coalesce(p_alasan,'Penugasan awal'));
  return jsonb_build_object('peringatan',nullif(v_risiko,''));
end $$;

create function api_terima(p_misi int, p_terima boolean, p_alasan text default null) returns void language plpgsql security definer set search_path=public as $$
declare ag agen%rowtype; m misi%rowtype;
begin
  perform fn_need('agen');
  select * into ag from agen where profile_id=auth.uid();
  select * into m from misi where id=p_misi for update;
  if m.agen_id is distinct from ag.id then raise exception 'Misi ini bukan penugasan Anda.'; end if;
  if m.status<>'Unleashed' or m.ditolak then raise exception 'Misi tidak dalam status menunggu penerimaan.'; end if;
  if p_terima then perform fn_set_status(p_misi,'Salvation','Diterima agen');
  else
    if nullif(trim(p_alasan),'') is null then raise exception 'Alasan penolakan wajib diisi.'; end if;
    update misi set ditolak=true, alasan_tolak=p_alasan where id=p_misi;
    update agen set tersedia=true where id=ag.id;
    insert into misi_log(misi_id,dari,ke,oleh,alasan) values(p_misi,'Unleashed','Unleashed',auth.uid(),'Ditolak agen: '||p_alasan);
    perform fn_audit('TOLAK_MISI','misi:'||p_misi,jsonb_build_object('alasan',p_alasan));
  end if;
end $$;

create function api_minta(p_misi int, p_items jsonb) returns void language plpgsql security definer set search_path=public as $$
declare ag agen%rowtype; m misi%rowtype; pid int; pst text;
begin
  perform fn_need('agen');
  select * into ag from agen where profile_id=auth.uid();
  select * into m from misi where id=p_misi for update;
  if m.agen_id is distinct from ag.id then raise exception 'Misi ini bukan penugasan Anda.'; end if;
  if m.status<>'Salvation' then raise exception 'Permintaan hanya bisa diajukan setelah misi diterima (status Salvation).'; end if;
  select id,status into pid,pst from permintaan where misi_id=p_misi;
  if pst is not null and pst<>'menunggu' then raise exception 'Permintaan sudah diproses logistik.'; end if;
  if pid is null then insert into permintaan(misi_id) values(p_misi) returning id into pid;
  else delete from permintaan_item where permintaan_id=pid; end if;
  insert into permintaan_item(permintaan_id,item_id,diminta)
    select pid,(e->>'item_id')::int,sum((e->>'jumlah')::int) from jsonb_array_elements(p_items) e where (e->>'jumlah')::int>0 group by (e->>'item_id')::int;
  insert into permintaan_item(permintaan_id,item_id,diminta) select pid,id,1 from item where kode_k3='METERAN' and aktif on conflict do nothing;
  if not exists(select 1 from permintaan_item where permintaan_id=pid) then raise exception 'Pilih minimal satu item.'; end if;
  perform fn_audit('MINTA_LOGISTIK','misi:'||p_misi,p_items);
end $$;

create function api_serahkan(p_misi int, p_items jsonb) returns jsonb language plpgsql security definer set search_path=public as $$
declare pid int; x record; v_req int; v_give int; kurang jsonb:='[]'; k jsonb; v_risiko text:=''; v_has int; m misi%rowtype; an analisis%rowtype;
begin
  perform fn_need('logistik');
  select id into pid from permintaan where misi_id=p_misi and status='menunggu' for update;
  if pid is null then raise exception 'Tidak ada permintaan yang menunggu untuk misi ini.'; end if;
  for x in select pi.item_id,pi.diminta,i.nama,i.jenis,i.stok_tersedia from permintaan_item pi join item i on i.id=pi.item_id where pi.permintaan_id=pid loop
    v_req := coalesce((select (e->>'jumlah')::int from jsonb_array_elements(p_items) e where (e->>'item_id')::int=x.item_id limit 1), x.diminta);
    v_give := greatest(0,least(v_req,x.diminta,x.stok_tersedia));
    if v_give>0 then perform fn_stok(x.item_id,'serah',v_give,-v_give,case when x.jenis='alat' then v_give else 0 end,p_misi,'Penyerahan ke agen'); end if;
    update permintaan_item set diserahkan=v_give where permintaan_id=pid and item_id=x.item_id;
    if v_give<x.diminta then kurang := kurang||jsonb_build_array(jsonb_build_object('item',x.nama,'diminta',x.diminta,'diserahkan',v_give)); end if;
  end loop;
  select * into m from misi where id=p_misi; select * into an from analisis where id=m.analisis_id;
  for k in select value from jsonb_array_elements(an.k3_cek) loop
    select coalesce(sum(pi.diserahkan),0) into v_has from permintaan_item pi where pi.permintaan_id=pid and pi.item_id=nullif(k->>'item_id','')::int;
    if v_has < (k->>'qty')::int then v_risiko := v_risiko||'Syarat K3 tidak terpenuhi: '||coalesce(k->>'item','?')||' (diserahkan '||v_has||' dari '||(k->>'qty')||'). '; end if;
  end loop;
  if not exists(select 1 from permintaan_item pi join item i on i.id=pi.item_id where pi.permintaan_id=pid and i.kode_k3='METERAN' and pi.diserahkan>0) then v_risiko := v_risiko||'Meteran Segel tidak diserahkan. '; end if;
  update misi set risiko=nullif(coalesce(risiko,'')||v_risiko,'') where id=p_misi;
  update permintaan set status='diserahkan', diproses_at=now(), diproses_oleh=auth.uid() where id=pid;
  perform fn_set_status(p_misi,'Exorcise','Perlengkapan diserahkan');
  return jsonb_build_object('kekurangan',kurang,'risiko_k3',nullif(v_risiko,''));
end $$;

create function api_simpan_percobaan(p_misi int, p_data jsonb) returns int language plpgsql security definer set search_path=public as $$
declare ag agen%rowtype; m misi%rowtype; v_id int;
begin
  perform fn_need('agen');
  select * into ag from agen where profile_id=auth.uid();
  select * into m from misi where id=p_misi;
  if m.agen_id is distinct from ag.id or m.status<>'Exorcise' then raise exception 'Simulasi hanya untuk misi Anda yang berstatus Exorcise.'; end if;
  insert into percobaan(misi_id,ke,berhasil,durasi,segel_akhir,resistensi_akhir,segel_nol,pakai,oleh)
  values(p_misi,coalesce((select max(ke) from percobaan where misi_id=p_misi),0)+1,(p_data->>'berhasil')::boolean,(p_data->>'durasi')::int,(p_data->>'segel_akhir')::int,
         (p_data->>'resistensi_akhir')::int,coalesce((p_data->>'segel_nol')::boolean,false),coalesce(p_data->'pakai','[]'),auth.uid()) returning id into v_id;
  return v_id;
end $$;

create function api_selesai(p_misi int, p_hasil jsonb) returns jsonb language plpgsql security definer set search_path=public as $$
declare m misi%rowtype; ag agen%rowtype; an analisis%rowtype; lap laporan%rowtype; tc tier_config%rowtype; x record;
  v_ok boolean; v_tangkap boolean; v_sifat text; v_acak jsonb:='{}'; r1 numeric; r2 numeric; v_pakai int; v_kond text; v_segel boolean;
  pen numeric; v_alat numeric:=0; v_perl numeric:=0; v_dasar numeric; v_gaji numeric; v_total numeric; v_um numeric; v_sel int; v_nama text; rinci jsonb;
begin
  perform fn_need('agen');
  select * into ag from agen where profile_id=auth.uid();
  select * into m from misi where id=p_misi for update;
  if m.agen_id is distinct from ag.id then raise exception 'Misi ini bukan penugasan Anda.'; end if;
  if m.status<>'Exorcise' then raise exception 'Hasil hanya dapat dilaporkan pada status Exorcise (saat ini: %).', m.status; end if;
  select * into an from analisis where id=m.analisis_id; select * into lap from laporan where id=m.laporan_id; select * into tc from tier_config where tier=an.tier;
  v_ok := coalesce((p_hasil->>'berhasil')::boolean,false);
  v_tangkap := v_ok and coalesce((p_hasil->>'ditangkap')::boolean,false);
  v_sifat := coalesce(nullif(p_hasil->>'sifat',''),'jahat');
  v_segel := coalesce((p_hasil->>'segel_nol')::boolean,false);
  if lap.entitas_kelas='unknown' then   -- entitas Unknown: hasil diundi, probabilitas dikonfigurasi & disimpan untuk audit
    r1 := random()*100; r2 := random()*100;
    v_ok := r1 < fn_cfg('peluang_unknown_berhasil');
    v_tangkap := v_ok and coalesce((p_hasil->>'ditangkap')::boolean,false);
    v_sifat := case when r2 < fn_cfg('peluang_unknown_jinak') then 'jinak' else 'jahat' end;
    v_acak := jsonb_build_object('undian_hasil',round(r1,2),'ambang_berhasil',fn_cfg('peluang_unknown_berhasil'),'undian_sifat',round(r2,2),'ambang_jinak',fn_cfg('peluang_unknown_jinak'),'input_agen_berhasil',p_hasil->'berhasil');
  end if;
  for x in select pi.item_id,pi.diserahkan,i.jenis,i.nama,i.biaya_pakai from permintaan_item pi join permintaan p on p.id=pi.permintaan_id join item i on i.id=pi.item_id where p.misi_id=p_misi and pi.diserahkan>0 loop
    if x.jenis='alat' then
      update permintaan_item set dipakai=x.diserahkan, dikembalikan=x.diserahkan where item_id=x.item_id and permintaan_id=(select id from permintaan where misi_id=p_misi);
      perform fn_stok(x.item_id,'kembali',x.diserahkan,x.diserahkan,-x.diserahkan,p_misi,'Alat kembali setelah misi');
      v_alat := v_alat + x.diserahkan*x.biaya_pakai;
    else
      v_pakai := coalesce((select sum((e->>'jumlah')::int) from jsonb_array_elements(coalesce(p_hasil->'pakai','[]')) e where (e->>'item_id')::int=x.item_id),0);
      if v_pakai > x.diserahkan then raise exception 'Pemakaian % (%) melebihi jumlah yang diserahkan (%).', x.nama, v_pakai, x.diserahkan; end if;
      update permintaan_item set dipakai=v_pakai, dikembalikan=x.diserahkan-v_pakai where item_id=x.item_id and permintaan_id=(select id from permintaan where misi_id=p_misi);
      if v_pakai>0 then perform fn_stok(x.item_id,'pakai',v_pakai,0,0,p_misi,'Pemakaian di lapangan'); end if;
      if x.diserahkan-v_pakai>0 then perform fn_stok(x.item_id,'kembali',x.diserahkan-v_pakai,x.diserahkan-v_pakai,0,p_misi,'Sisa perlengkapan dikembalikan'); end if;
      v_perl := v_perl + v_pakai*x.biaya_pakai;
    end if;
  end loop;
  update percobaan set final=false where misi_id=p_misi;
  update percobaan set final=true where id=nullif(p_hasil->>'percobaan_id','')::int and misi_id=p_misi;
  insert into hasil_misi(misi_id,berhasil,entitas_dilawan,ditangkap,nama_ditangkap,sifat,kondisi_agen,catatan,segel_nol,percobaan_id,acak)
  values(p_misi,v_ok,p_hasil->>'entitas_dilawan',v_tangkap,p_hasil->>'nama_ditangkap',case when v_tangkap then v_sifat end,p_hasil->>'kondisi_agen',p_hasil->>'catatan',v_segel,nullif(p_hasil->>'percobaan_id','')::int,v_acak);
  perform fn_set_status(p_misi,case when v_ok then 'Contain' else 'Death' end::status_misi,case when v_ok then 'Misi berhasil' else 'Misi gagal, agen dinyatakan meninggal' end);
  if v_ok then
    v_kond := coalesce(nullif(p_hasil->>'kondisi_agen',''),'Sehat');
    update agen set kondisi=v_kond, tersedia=(v_kond in ('Sehat','Luka Ringan')) where id=ag.id;
  else update agen set kondisi='Meninggal', aktif=false, tersedia=false where id=ag.id; end if;
  if v_tangkap then
    v_nama := coalesce(nullif(p_hasil->>'nama_ditangkap',''),nullif(p_hasil->>'entitas_dilawan',''),'Entitas tanpa nama');
    if v_sifat='jinak' then
      insert into entitas(misi_id,nama,kelas,tier,sifat,lokasi,status) values(p_misi,v_nama,lap.entitas_kelas,an.tier,'jinak','koleksi','Jinak');
    else
      v_sel := fn_sel_kosong();
      insert into entitas(misi_id,nama,kelas,tier,sifat,lokasi,sel_no,status,segel_at)
      values(p_misi,v_nama,lap.entitas_kelas,an.tier,'jahat',case when v_sel is null then 'antre' else 'sel' end,v_sel,case when v_sel is null then 'Antre' else 'Tertahan' end,now());
    end if;
  end if;
  -- invoice dari biaya AKTUAL
  v_dasar := tc.tarif;
  pen := least(100,(case when not v_ok then fn_cfg('penalti_gagal_persen') else 0 end)+(case when v_segel then fn_cfg('penalti_segel_persen') else 0 end));
  v_gaji := v_dasar*fn_cfg('jasa_agen_persen')/100*(1-pen/100);
  v_total := round((an.estimasi->>'analisis')::numeric+v_dasar+(an.estimasi->>'darurat')::numeric+v_alat+v_perl-v_dasar*pen/100,0);
  select coalesce(sum(jumlah),0) into v_um from pembayaran where misi_id=p_misi and jenis='uang_muka';
  rinci := jsonb_build_array(jsonb_build_object('kelompok','Biaya analisis','label','Analisis bahaya','nilai',(an.estimasi->>'analisis')::numeric),
                             jsonb_build_object('kelompok','Jasa agen','label','Tarif dasar Tier '||an.tier,'nilai',v_dasar));
  if (an.estimasi->>'darurat')::numeric>0 then rinci := rinci||jsonb_build_array(jsonb_build_object('kelompok','Biaya darurat','label','Layanan ekspres/darurat','nilai',(an.estimasi->>'darurat')::numeric)); end if;
  rinci := rinci || coalesce((select jsonb_agg(jsonb_build_object('kelompok',case i.jenis when 'alat' then 'Alat (biaya pakai)' else 'Perlengkapan' end,
        'label',i.nama||' x'||(case i.jenis when 'alat' then pi.diserahkan else pi.dipakai end),'nilai',(case i.jenis when 'alat' then pi.diserahkan else pi.dipakai end)*i.biaya_pakai))
        from permintaan_item pi join permintaan p on p.id=pi.permintaan_id join item i on i.id=pi.item_id
        where p.misi_id=p_misi and ((i.jenis='alat' and pi.diserahkan>0) or (i.jenis='perlengkapan' and pi.dipakai>0))),'[]');
  if pen>0 then rinci := rinci||jsonb_build_array(jsonb_build_object('kelompok','Penyesuaian','label',trim(both ' ' from (case when not v_ok then 'Misi gagal ' else '' end)||(case when v_segel then 'Segel habis ' else '' end))||'- potongan jasa '||pen||'%','nilai',-round(v_dasar*pen/100,0))); end if;
  insert into invoice(misi_id,nomor,rincian,total,uang_muka,sisa,biaya_operasional)
  values(p_misi,'INV-'||to_char(now(),'YYMMDD')||'-'||lpad(p_misi::text,4,'0'),rinci,v_total,v_um,v_total-v_um,round(v_gaji+v_alat+v_perl,0));
  perform fn_audit('HASIL_MISI','misi:'||p_misi,jsonb_build_object('berhasil',v_ok,'ditangkap',v_tangkap,'sifat',v_sifat,'segel_nol',v_segel,'acak',v_acak));
  return jsonb_build_object('status',case when v_ok then 'Contain' else 'Death' end,'total',v_total,'sisa',v_total-v_um,'acak',v_acak);
end $$;

create function api_batalkan(p_misi int, p_alasan text) returns void language plpgsql security definer set search_path=public as $$
declare m misi%rowtype;
begin
  perform fn_need('operator');
  if nullif(trim(p_alasan),'') is null then raise exception 'Alasan pembatalan wajib diisi.'; end if;
  select * into m from misi where id=p_misi for update;
  if m.status not in ('Whisper','Unleashed','Salvation') then raise exception 'Misi berstatus % tidak dapat dibatalkan.', m.status; end if;
  update permintaan set status='dibatalkan' where misi_id=p_misi and status='menunggu';
  if m.agen_id is not null then update agen set tersedia=true where id=m.agen_id and aktif and kondisi in ('Sehat','Luka Ringan'); end if;
  if m.uang_muka>0 then insert into pembayaran(misi_id,jenis,jumlah,metode,oleh) values(p_misi,'pengembalian',-m.uang_muka,'Pengembalian uang muka',auth.uid()); end if;
  perform fn_set_status(p_misi,'Dibatalkan','Pembatalan administratif: '||p_alasan);
end $$;

create function api_bayar_lunas(p_misi int, p_metode text) returns jsonb language plpgsql security definer set search_path=public as $$
declare inv invoice%rowtype; m misi%rowtype; sert text;
begin
  perform fn_need('kepala');
  select * into inv from invoice where misi_id=p_misi for update;
  if not found then raise exception 'Invoice belum terbit (misi belum selesai).'; end if;
  if inv.status='lunas' then raise exception 'Invoice % sudah lunas.', inv.nomor; end if;
  select * into m from misi where id=p_misi;
  insert into pembayaran(misi_id,jenis,jumlah,metode,oleh) values(p_misi,case when inv.sisa>=0 then 'pelunasan' else 'pengembalian' end,inv.sisa,coalesce(p_metode,'Transfer'),auth.uid());
  if m.status='Contain' then sert := 'SRT-'||to_char(now(),'YYMMDD')||'-'||lpad(p_misi::text,4,'0'); end if;
  update invoice set status='lunas', lunas_at=now(), sertifikat_nomor=sert where id=inv.id;
  perform fn_audit('PELUNASAN','misi:'||p_misi,jsonb_build_object('jumlah',inv.sisa));
  return jsonb_build_object('sertifikat',sert);
end $$;

-- ---------------------------------------------------------------- logistik
create function api_stok_masuk(p_item int, p_jumlah int, p_alasan text) returns void language plpgsql security definer set search_path=public as $$
begin
  perform fn_need('logistik');
  if coalesce(p_jumlah,0)<=0 then raise exception 'Jumlah harus lebih dari 0.'; end if;
  perform fn_stok(p_item,'masuk',p_jumlah,p_jumlah,0,null,coalesce(nullif(p_alasan,''),'Stok masuk'));
end $$;

create function api_stok_koreksi(p_item int, p_baru int, p_alasan text) returns void language plpgsql security definer set search_path=public as $$
declare cur int;
begin
  perform fn_need('logistik');
  if nullif(trim(p_alasan),'') is null then raise exception 'Alasan koreksi wajib diisi.'; end if;
  if p_baru<0 then raise exception 'Stok tidak boleh negatif.'; end if;
  select stok_tersedia into cur from item where id=p_item;
  if p_baru=cur then raise exception 'Tidak ada perubahan.'; end if;
  perform fn_stok(p_item,'koreksi',abs(p_baru-cur),p_baru-cur,0,null,p_alasan);
end $$;

-- ---------------------------------------------------------------- karantina & rehabilitasi
create function fn_promosi_antre() returns void language plpgsql security definer set search_path=public as $$
declare e record; s int;
begin
  for e in select id from entitas where lokasi='antre' order by id loop
    s := fn_sel_kosong(); exit when s is null;
    update entitas set lokasi='sel', sel_no=s, status='Tertahan', segel_at=now() where id=e.id;
  end loop;
end $$;

create function api_rehab_sweep() returns int language plpgsql security definer set search_path=public as $$
declare n int;
begin
  perform fn_need('kepala','logistik');
  update entitas set sifat='jinak', lokasi='koleksi', sel_no=null, status='Jinak' where status='Rehabilitasi' and rehab_selesai<=now();
  get diagnostics n = row_count;
  if n>0 then perform fn_promosi_antre(); perform fn_audit('REHAB_SELESAI',null,jsonb_build_object('jumlah',n)); end if;
  return n;
end $$;

create function api_rehab_mulai(p_entitas int) returns void language plpgsql security definer set search_path=public as $$
declare e entitas%rowtype; it int; tc tier_config%rowtype;
begin
  perform fn_need('kepala');
  select * into e from entitas where id=p_entitas for update;
  if e.lokasi<>'sel' or e.status<>'Tertahan' then raise exception 'Hanya entitas jahat yang berada di sel (status Tertahan) yang dapat direhabilitasi.'; end if;
  select id into it from item where kode_k3='SEGEL_REHAB' and aktif limit 1;
  if it is null then raise exception 'Item perlengkapan penjaga segel (kode_k3 SEGEL_REHAB) belum dipetakan.'; end if;
  perform fn_stok(it,'pakai',1,-1,0,e.misi_id,'Penjaga segel rehabilitasi entitas '||e.nama);
  select * into tc from tier_config where tier=coalesce(e.tier,1);
  update entitas set status='Rehabilitasi', rehab_mulai=now(), rehab_selesai=now()+make_interval(mins=>tc.rehab_menit), segel_at=now() where id=p_entitas;
  perform fn_audit('REHAB_MULAI','entitas:'||p_entitas,jsonb_build_object('menit',tc.rehab_menit));
end $$;

create function api_segel_ulang(p_entitas int) returns void language plpgsql security definer set search_path=public as $$
declare e entitas%rowtype; it int;
begin
  perform fn_need('kepala','logistik');
  select * into e from entitas where id=p_entitas for update;
  if e.lokasi<>'sel' then raise exception 'Entitas tidak berada di sel.'; end if;
  select id into it from item where kode_k3='SEGEL_REHAB' and aktif limit 1;
  perform fn_stok(it,'pakai',1,-1,0,e.misi_id,'Pembaruan segel sel '||e.sel_no);
  update entitas set segel_at=now() where id=p_entitas;
  perform fn_audit('SEGEL_ULANG','entitas:'||p_entitas,null);
end $$;

-- ---------------------------------------------------------------- master data (hanya Kepala)
create function api_master_simpan(p_tabel text, p_data jsonb) returns void language plpgsql security definer set search_path=public as $$
declare pk text; cols text; upd text; d jsonb := p_data - 'stok_tersedia' - 'stok_dipinjam' - 'dibuat_at';
begin
  perform fn_need('kepala');
  pk := case p_tabel when 'katalog_anomali' then 'id' when 'item' then 'id' when 'agen' then 'id' when 'konfigurasi' then 'kunci' when 'faktor_skor' then 'kategori,kode' when 'tier_config' then 'tier' end;
  if pk is null then raise exception 'Tabel master tidak dikenal.'; end if;
  select string_agg(quote_ident(k),','), string_agg(format('%1$I=excluded.%1$I',k),',') filter (where k <> all(string_to_array(pk,',')))
    into cols, upd from jsonb_object_keys(d) k;
  execute format('insert into %1$I (%2$s) select %2$s from jsonb_populate_record(null::%1$I,$1) on conflict (%3$s) do %4$s',
    p_tabel, cols, pk, case when upd is null then 'nothing' else 'update set '||upd end) using d;
  perform fn_audit('MASTER_'||upper(p_tabel),null,d);
end $$;
