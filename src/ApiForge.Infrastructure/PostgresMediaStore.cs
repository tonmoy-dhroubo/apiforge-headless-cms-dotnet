using ApiForge.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace ApiForge.Infrastructure;

public sealed class PostgresMediaStore(IWebHostEnvironment environment, IConfiguration configuration) : IMediaStore
{
    private readonly string _cs = configuration["Storage:ConnectionString"] ?? throw new InvalidOperationException("Storage:ConnectionString is required");
    private string Root => Path.Combine(environment.ContentRootPath, "uploads");
    private NpgsqlConnection Open() { var c = new NpgsqlConnection(_cs); c.Open(); return c; }
    public async Task<MediaRecord> Save(IFormFile file, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Root); var name = Path.GetFileName(file.FileName); var ext = Path.GetExtension(name); var hash = Guid.NewGuid().ToString(); var stored = hash + ext; var path = Path.Combine(Root, stored);
        await using (var output = File.Create(path)) await file.CopyToAsync(output, ct);
        try
        {
            await using var c = Open(); await using var q = new NpgsqlCommand("INSERT INTO media(name,hash,ext,mime,size,url,provider) VALUES(@n,@h,@e,@m,@s,@u,'local') RETURNING id,created_at", c);
            q.Parameters.AddWithValue("n", name); q.Parameters.AddWithValue("h", hash); q.Parameters.AddWithValue("e", ext); q.Parameters.AddWithValue("m", (object?)file.ContentType ?? DBNull.Value); q.Parameters.AddWithValue("s", file.Length / 1024d); q.Parameters.AddWithValue("u", "/api/upload/files/" + stored);
            await using var r = await q.ExecuteReaderAsync(ct); await r.ReadAsync(ct); var id = r.GetInt64(0); return new(id, name, null, null, null, null, hash, ext, file.ContentType, file.Length / 1024d, "/api/upload/files/" + stored, "local", path);
        }
        catch { try { File.Delete(path); } catch { } throw; }
    }
    private MediaRecord Read(NpgsqlDataReader r) { var hash = r.GetString(6); var ext = r.GetString(7); return new(r.GetInt64(0), r.IsDBNull(1) ? "upload" : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetInt32(4), r.IsDBNull(5) ? null : r.GetInt32(5), hash, ext, r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? 0 : Convert.ToDouble(r.GetValue(9)), r.IsDBNull(10) ? "/api/upload/files/" + hash + ext : r.GetString(10), r.IsDBNull(11) ? "local" : r.GetString(11), Path.Combine(Root, hash + ext)); }
    private const string Select = "SELECT id,name,alternative_text,caption,width,height,hash,ext,mime,size,url,provider FROM media";
    public async Task<IReadOnlyList<MediaRecord>> All(CancellationToken ct = default) { await using var c=Open(); await using var q=new NpgsqlCommand(Select+" ORDER BY id",c); await using var r=await q.ExecuteReaderAsync(ct); var x=new List<MediaRecord>(); while(await r.ReadAsync(ct))x.Add(Read(r)); return x; }
    public async Task<MediaRecord?> ById(long id,CancellationToken ct=default){await using var c=Open();await using var q=new NpgsqlCommand(Select+" WHERE id=@id",c);q.Parameters.AddWithValue("id",id);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Read(r):null;}
    public async Task<MediaRecord?> ByFile(string filename,CancellationToken ct=default){var ext=Path.GetExtension(filename);var hash=Path.GetFileNameWithoutExtension(filename);await using var c=Open();await using var q=new NpgsqlCommand(Select+" WHERE hash=@h AND ext=@e",c);q.Parameters.AddWithValue("h",hash);q.Parameters.AddWithValue("e",ext);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Read(r):null;}
    public async Task<bool> Remove(long id,CancellationToken ct=default){var old=await ById(id,ct);if(old is null)return false;await using var c=Open();await using var q=new NpgsqlCommand("DELETE FROM media WHERE id=@id",c);q.Parameters.AddWithValue("id",id);var ok=await q.ExecuteNonQueryAsync(ct)>0;try{File.Delete(old.Path);}catch{}return ok;}
}
