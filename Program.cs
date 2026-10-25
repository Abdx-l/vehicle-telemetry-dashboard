using Microsoft.EntityFrameworkCore;
if(args.Contains("--simulate")){using var client=new HttpClient{BaseAddress=new Uri(Environment.GetEnvironmentVariable("API_URL")??"http://localhost:5000")};for(int tick=0;tick<600;tick++){var batch=Enumerable.Range(0,50).Select(i=>new Reading{Vehicle="V"+i,Lat=43.6+i*.001+tick*.00001,Lon=-79.6+i*.001,Speed=tick%120<90?40:0,Rpm=1800,Timestamp=DateTime.UtcNow}).ToArray();(await client.PostAsJsonAsync("/api/readings",batch)).EnsureSuccessStatusCode();await Task.Delay(1000);}return;}
var builder=WebApplication.CreateBuilder(args);builder.Services.AddDbContext<TelemetryDb>(o=>o.UseSqlite("Data Source=telemetry.db"));var app=builder.Build();using(var s=app.Services.CreateScope())s.ServiceProvider.GetRequiredService<TelemetryDb>().Database.EnsureCreated();
var gate=new SemaphoreSlim(1);
app.MapPost("/api/readings",async(Reading[] readings,TelemetryDb db)=>{if(readings.Length>1000||readings.Any(r=>r.Lat< -90||r.Lat>90||r.Lon< -180||r.Lon>180||!double.IsFinite(r.Lat)||!double.IsFinite(r.Lon)||r.Speed<0||r.Speed>300||!double.IsFinite(r.Speed)||string.IsNullOrWhiteSpace(r.Vehicle)))return Results.BadRequest();
await gate.WaitAsync();try{foreach(var r in readings.OrderBy(r=>r.Timestamp)){
 var previous=await db.Readings.Where(x=>x.Vehicle==r.Vehicle).OrderByDescending(x=>x.Timestamp).FirstOrDefaultAsync();if(previous!=null&&r.Timestamp<=previous.Timestamp)continue;
 var trip=await db.Trips.FirstOrDefaultAsync(t=>t.Vehicle==r.Vehicle&&t.End==null);
 if(trip==null&&r.Speed>5){trip=new Trip{Vehicle=r.Vehicle,Start=r.Timestamp,LastMoving=r.Timestamp};db.Trips.Add(trip);}
 if(trip!=null){if(previous!=null){double seconds=(r.Timestamp-previous.Timestamp).TotalSeconds;if(seconds<300){trip.DistanceKm+=Geo.Distance(previous.Lat,previous.Lon,r.Lat,r.Lon);if(previous.Speed<=5)trip.IdleSeconds+=seconds;}}if(r.Speed>5)trip.LastMoving=r.Timestamp;else if((r.Timestamp-trip.LastMoving).TotalSeconds>=300)trip.End=r.Timestamp;}
 db.Readings.Add(r);await db.SaveChangesAsync();
}return Results.Accepted();}finally{gate.Release();}});
app.MapGet("/api/vehicles",async(TelemetryDb db)=>(await db.Readings.OrderByDescending(r=>r.Timestamp).Take(10000).ToListAsync()).GroupBy(r=>r.Vehicle).Select(g=>g.First()));
app.MapGet("/api/trips/{vehicle}",async(string vehicle,TelemetryDb db)=>await db.Trips.Where(t=>t.Vehicle==vehicle).OrderByDescending(t=>t.Start).ToListAsync());app.Run();
public class Reading{public int Id{get;set;}public string Vehicle{get;set;}="";public double Lat{get;set;}public double Lon{get;set;}public double Speed{get;set;}public int Rpm{get;set;}public DateTime Timestamp{get;set;}}
public class Trip{public int Id{get;set;}public string Vehicle{get;set;}="";public DateTime Start{get;set;}public DateTime LastMoving{get;set;}public DateTime?End{get;set;}public double DistanceKm{get;set;}public double IdleSeconds{get;set;}}
public class TelemetryDb(DbContextOptions<TelemetryDb>o):DbContext(o){public DbSet<Reading>Readings=>Set<Reading>();public DbSet<Trip>Trips=>Set<Trip>();protected override void OnModelCreating(ModelBuilder m){m.Entity<Reading>().HasIndex(r=>new{r.Vehicle,r.Timestamp}).IsUnique();}}
public static class Geo{public static double Distance(double a,double b,double c,double d){double rad=Math.PI/180,dl=(c-a)*rad,dn=(d-b)*rad;double h=Math.Pow(Math.Sin(dl/2),2)+Math.Cos(a*rad)*Math.Cos(c*rad)*Math.Pow(Math.Sin(dn/2),2);return 6371*2*Math.Asin(Math.Sqrt(Math.Clamp(h,0,1)));}}
